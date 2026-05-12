using Finance.Application.Commands;
using Finance.Application.Dtos;
using Finance.Application.Ports;
using Finance.Application.Queries;
using Finance.Application.Repositories;
using Finance.Application.Services;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Finance.Application.Managers;

/// <summary>
/// Owns the full FinancialConnection command surface: link lifecycle, cursor-based
/// transaction sync, and recurring / bank-sync suggestion management.
/// Single change driver: bank data provider API contract (Plaid Link, sync, recurring streams).
/// </summary>
internal sealed class FinancialConnectionManager : IFinancialConnectionManager
{
    private readonly IBankDataProvider _api;
    private readonly IFinancialConnectionRepository _repo;
    private readonly IFinancialConnectionQuery _connectionQuery;
    private readonly IConnectionTokenProtector _tokenProtector;
    private readonly IExpenseRepository _expenseRepository;
    private readonly IExpensePaymentRepository _expensePaymentRepository;
    private readonly IIncomeSourceRepository _incomeRepository;
    private readonly IBankSyncService _syncService;
    private readonly ILogger<FinancialConnectionManager> _logger;

    public FinancialConnectionManager(
        IBankDataProvider api,
        IFinancialConnectionRepository repo,
        IFinancialConnectionQuery connectionQuery,
        IConnectionTokenProtector tokenProtector,
        IExpenseRepository expenseRepository,
        IExpensePaymentRepository expensePaymentRepository,
        IIncomeSourceRepository incomeRepository,
        IBankSyncService syncService,
        ILogger<FinancialConnectionManager> logger)
    {
        _api = api;
        _repo = repo;
        _connectionQuery = connectionQuery;
        _tokenProtector = tokenProtector;
        _expenseRepository = expenseRepository;
        _expensePaymentRepository = expensePaymentRepository;
        _incomeRepository = incomeRepository;
        _syncService = syncService;
        _logger = logger;
    }

    // ── Link token ──────────────────────────────────────────────────────────

    public async Task<LinkTokenDto> CreateLinkTokenAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var result = await _api.CreateLinkTokenAsync(userId, cancellationToken);
        return new LinkTokenDto(result.LinkToken, result.Expiration);
    }

    // ── Exchange public_token → access_token ────────────────────────────────

    public async Task<ConnectionDto> ExchangePublicTokenAsync(
        Guid userId, LinkConnectionCommand request, CancellationToken cancellationToken = default)
    {
        var exchange = await _api.ExchangePublicTokenAsync(request.PublicToken, cancellationToken);

        // Idempotency: if the user re-links the same institution, update in place
        // rather than creating a duplicate. Plaid issues a new access_token on every
        // exchange, so we always store the freshest one.
        var existing = await _repo.GetConnectionByExternalIdAsync(exchange.ItemId, cancellationToken);
        var encrypted = _tokenProtector.Protect(exchange.AccessToken);
        FinancialConnection connection;

        if (existing is not null)
        {
            // Clean up expenses/income that were auto-created from this connection's suggestions
            // before removing the connection (same logic as DisconnectAsync).
            var oldSuggestions = await _connectionQuery.ListSuggestionsForConnectionAsync(existing.Id, cancellationToken);
            foreach (var suggestion in oldSuggestions.Where(s => s.IsLinked && s.LinkedEntityId.HasValue))
            {
                if (suggestion.LinkedEntityType == "Expense")
                {
                    var expense = await _expenseRepository.GetByIdAsync(
                        ExpenseId.Create(suggestion.LinkedEntityId!.Value), cancellationToken);
                    if (expense is not null) await _expenseRepository.RemoveAsync(expense, cancellationToken);
                }
                else if (suggestion.LinkedEntityType == "IncomeSource")
                {
                    var income = await _incomeRepository.GetByIdAsync(
                        IncomeId.Create(suggestion.LinkedEntityId!.Value), cancellationToken);
                    if (income is not null) await _incomeRepository.RemoveAsync(income, cancellationToken);
                }
            }

            // Model "re-link" as remove + add because FinancialConnection has no public
            // setter for EncryptedAccessToken (intentionally invariant-protected).
            await _repo.RemoveConnectionAsync(existing, cancellationToken);
        }

        connection = FinancialConnection.Connect(
            UserId.Create(userId),
            exchange.ItemId,
            request.InstitutionName ?? "Unknown",
            request.InstitutionId,
            encrypted);

        await _repo.AddConnectionAsync(connection, cancellationToken);
        await _repo.CommitAsync(cancellationToken);

        // Fetch and persist accounts immediately so the UI has something to show.
        var accountsResult = await _api.GetAccountsAsync(exchange.AccessToken, cancellationToken);
        var persistedAccounts = new List<FinancialAccount>(accountsResult.Accounts.Count);
        foreach (var dto in accountsResult.Accounts)
        {
            var account = FinancialAccount.Create(
                connection.Id,
                connection.UserId,
                dto.AccountId,
                dto.Name,
                dto.OfficialName,
                dto.Mask,
                dto.Type,
                dto.Subtype,
                dto.CurrencyCode,
                dto.CurrentBalance,
                dto.AvailableBalance);
            await _repo.AddAccountAsync(account, cancellationToken);
            persistedAccounts.Add(account);
        }
        await _repo.CommitAsync(cancellationToken);

        // Kick off an initial sync so the UI sees transactions on first paint.
        try
        {
            await _syncService.SyncConnectionAsync(connection, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Initial sync after linking connection {ConnectionId} failed; will be retried on webhook.",
                connection.ExternalId);
        }

        return new ConnectionDto(
            connection.Id.Value,
            connection.InstitutionName,
            connection.Status.ToString(),
            connection.LastSyncedAt,
            connection.CreatedAt,
            persistedAccounts.Select(a => new LinkedAccountDto(
                a.Id, a.Name, a.OfficialName, a.Mask, a.Type, a.Subtype,
                a.CurrencyCode, a.CurrentBalance, a.AvailableBalance)).ToList());
    }

    // ── Unlink ──────────────────────────────────────────────────────────────

    public async Task DisconnectAsync(
        Guid userId, DisconnectCommand request, CancellationToken cancellationToken = default)
    {
        var connection = await _repo.GetConnectionAsync(FinancialConnectionId.Create(request.ConnectionId), cancellationToken);
        if (connection is null) return;
        if (connection.UserId.Value != userId)
            throw new UnauthorizedAccessException("Access denied.");

        try
        {
            var accessToken = _tokenProtector.Unprotect(connection.EncryptedAccessToken);
            await _api.RemoveItemAsync(accessToken, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Provider item removal failed for connection {ConnectionId}", connection.ExternalId);
        }

        // Remove domain entities that were auto-created from this connection's suggestions.
        var suggestions = await _connectionQuery.ListSuggestionsForConnectionAsync(connection.Id, cancellationToken);
        foreach (var suggestion in suggestions.Where(s => s.IsLinked && s.LinkedEntityId.HasValue))
        {
            if (suggestion.LinkedEntityType == "Expense")
            {
                var expense = await _expenseRepository.GetByIdAsync(
                    ExpenseId.Create(suggestion.LinkedEntityId!.Value), cancellationToken);
                if (expense is not null)
                    await _expenseRepository.RemoveAsync(expense, cancellationToken);
            }
            else if (suggestion.LinkedEntityType == "IncomeSource")
            {
                var income = await _incomeRepository.GetByIdAsync(
                    IncomeId.Create(suggestion.LinkedEntityId!.Value), cancellationToken);
                if (income is not null)
                    await _incomeRepository.RemoveAsync(income, cancellationToken);
            }
        }

        // Removing the connection cascades to accounts, transactions, and suggestions at the DB level.
        connection.MarkRevoked();
        await _repo.RemoveConnectionAsync(connection, cancellationToken);
        await _repo.CommitAsync(cancellationToken);
    }

    // ── Sync ────────────────────────────────────────────────────────────────

    public async Task<SyncConnectionDto> SyncAsync(
        Guid userId, SyncConnectionCommand request, CancellationToken cancellationToken = default)
    {
        var connection = await _repo.GetConnectionAsync(
            FinancialConnectionId.Create(request.ConnectionId), cancellationToken)
            ?? throw new KeyNotFoundException("Financial connection not found.");

        if (connection.UserId.Value != userId)
            throw new UnauthorizedAccessException("Access denied.");

        var (added, modified, removed, hasMore) = await _syncService.SyncConnectionAsync(connection, cancellationToken);
        return new SyncConnectionDto(connection.Id.Value, added, modified, removed, hasMore, DateTime.UtcNow);
    }

    public async Task SyncByExternalItemIdAsync(
        string externalItemId, CancellationToken cancellationToken = default)
    {
        var connection = await _repo.GetConnectionByExternalIdAsync(externalItemId, cancellationToken);
        if (connection is null)
        {
            _logger.LogWarning("Webhook received for unknown provider item id {ExternalItemId}", externalItemId);
            return;
        }
        connection.RecordWebhook();
        await _syncService.SyncConnectionAsync(connection, cancellationToken);
    }

    // ── Recurring suggestions ────────────────────────────────────────────────

    public async Task<ListSuggestionsDto> RefreshSuggestionsAsync(
        Guid userId, RefreshSuggestionsCommand request, CancellationToken ct = default)
    {
        var connection = await _repo.GetConnectionAsync(
            FinancialConnectionId.Create(request.ConnectionId), ct)
            ?? throw new KeyNotFoundException("Financial connection not found.");
        if (connection.UserId.Value != userId)
            throw new UnauthorizedAccessException("Access denied.");

        await _syncService.RefreshSuggestionsAsync(connection, ct);

        var suggestions = await _connectionQuery.ListSuggestionsForUserAsync(UserId.Create(userId), ct);
        return new ListSuggestionsDto(suggestions.Select(MapSuggestion).ToList());
    }

    public async Task<AcceptSuggestionDto> AcceptSuggestionAsync(
        Guid userId, AcceptSuggestionCommand request, CancellationToken ct = default)
    {
        var suggestion = await _repo.GetSuggestionAsync(request.SuggestionId, ct)
            ?? throw new KeyNotFoundException("Recurring suggestion not found.");

        if (suggestion.UserId.Value != userId)
            throw new UnauthorizedAccessException("Access denied.");

        if (suggestion.IsLinked && suggestion.LinkedEntityId.HasValue && !string.IsNullOrEmpty(suggestion.LinkedEntityType))
            return new AcceptSuggestionDto(suggestion.Id, suggestion.LinkedEntityId.Value, suggestion.LinkedEntityType);

        var schedule = RecurrenceSchedule.Create(suggestion.Frequency, suggestion.FirstDate);
        var sourceName = !string.IsNullOrWhiteSpace(suggestion.MerchantName) ? suggestion.MerchantName
            : !string.IsNullOrWhiteSpace(suggestion.Description) ? suggestion.Description
            : "Unknown";

        if (suggestion.Direction == RecurringFlowDirection.Inflow)
        {
            var income = IncomeSource.Create(
                UserId.Create(userId), suggestion.AverageAmount,
                sourceName, schedule,
                paymentFrequency: suggestion.Frequency,
                lastPaymentDate: suggestion.LastDate);
            await _incomeRepository.AddAsync(income, ct);
            suggestion.MarkLinked(income.Id.Value, "IncomeSource");
            await _repo.SaveSuggestionAsync(suggestion, ct);
            await _repo.CommitAsync(ct);
            return new AcceptSuggestionDto(suggestion.Id, income.Id.Value, "IncomeSource");
        }
        else
        {
            var nextDue = suggestion.PredictedNextDate ?? suggestion.LastDate.AddDays(1);
            if (nextDue < DateTime.UtcNow.Date)
                nextDue = DateTime.UtcNow.Date.AddDays(1);

            var expense = Expense.Create(
                UserId.Create(userId), sourceName, suggestion.AverageAmount,
                ExpenseCategory.Other, nextDue, schedule);
            await _expenseRepository.AddAsync(expense, ct);
            suggestion.MarkLinked(expense.Id.Value, "Expense");
            await _repo.SaveSuggestionAsync(suggestion, ct);
            await _repo.CommitAsync(ct);
            return new AcceptSuggestionDto(suggestion.Id, expense.Id.Value, "Expense");
        }
    }

    // ── Bank-sync suggestions ────────────────────────────────────────────────

    public async Task<AcceptSuggestionDto> AcceptBankSyncSuggestionAsync(
        Guid userId, AcceptBankSyncSuggestionCommand request, CancellationToken ct = default)
    {
        var suggestion = await _repo.GetBankSyncSuggestionAsync(request.SuggestionId, ct)
            ?? throw new KeyNotFoundException("Bank sync suggestion not found.");

        if (suggestion.UserId.Value != userId)
            throw new UnauthorizedAccessException("Access denied.");

        if (suggestion.IsLinked && suggestion.LinkedEntityId.HasValue && !string.IsNullOrEmpty(suggestion.LinkedEntityType))
            return new AcceptSuggestionDto(suggestion.Id, suggestion.LinkedEntityId.Value, suggestion.LinkedEntityType);

        var displayName = !string.IsNullOrWhiteSpace(suggestion.MerchantName)
            ? suggestion.MerchantName : suggestion.Name;
        var amount = Money.Create(suggestion.Amount, suggestion.Currency);

        if (request.AsIncome)
        {
            var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, suggestion.TransactionDate);
            var income = IncomeSource.Create(
                UserId.Create(userId), amount, displayName, schedule,
                paymentFrequency: RecurrenceFrequency.Monthly,
                lastPaymentDate: suggestion.TransactionDate);
            await _incomeRepository.AddAsync(income, ct);
            suggestion.MarkLinked(income.Id.Value, "IncomeSource");
            await _repo.SaveBankSyncSuggestionAsync(suggestion, ct);
            await _repo.CommitAsync(ct);
            return new AcceptSuggestionDto(suggestion.Id, income.Id.Value, "IncomeSource");
        }
        else
        {
            var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, suggestion.TransactionDate);
            var nextDue = suggestion.TransactionDate.AddMonths(1);
            if (nextDue < DateTime.UtcNow.Date)
                nextDue = DateTime.UtcNow.Date.AddDays(1);

            Expense expense;
            if (request.HouseholdId.HasValue)
            {
                expense = Expense.CreateHousehold(
                    HouseholdId.Create(request.HouseholdId.Value),
                    UserId.Create(userId),
                    displayName, amount, ExpenseCategory.Other, nextDue, schedule);
                expense.Activate();
            }
            else
            {
                expense = Expense.Create(
                    UserId.Create(userId), displayName, amount,
                    ExpenseCategory.Other, nextDue, schedule);
            }

            var payment = ExpensePayment.Create(
                expense.Id, expense.UserId,
                suggestion.TransactionDate, suggestion.ExternalTransactionId);
            await _expenseRepository.AddAsync(expense, ct);
            await _expensePaymentRepository.AddAsync(payment, ct);
            suggestion.MarkLinked(expense.Id.Value, "Expense");
            await _repo.SaveBankSyncSuggestionAsync(suggestion, ct);
            await _repo.CommitAsync(ct);
            return new AcceptSuggestionDto(suggestion.Id, expense.Id.Value, "Expense");
        }
    }

    public async Task DismissBankSyncSuggestionAsync(
        Guid userId, DismissBankSyncSuggestionCommand request, CancellationToken ct = default)
    {
        var suggestion = await _repo.GetBankSyncSuggestionAsync(request.SuggestionId, ct)
            ?? throw new KeyNotFoundException("Bank sync suggestion not found.");

        if (suggestion.UserId.Value != userId)
            throw new UnauthorizedAccessException("Access denied.");

        suggestion.Dismiss();
        await _repo.SaveBankSyncSuggestionAsync(suggestion, ct);
        await _repo.CommitAsync(ct);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static RecurringSuggestionDto MapSuggestion(RecurringSuggestion s) => new(
        s.Id, s.FinancialConnectionId.Value, s.AccountId,
        s.Direction.ToString(), s.Description, s.MerchantName,
        s.Frequency, s.AverageAmount.Amount, s.LastAmount.Amount, s.AverageAmount.Currency,
        s.FirstDate, s.LastDate, s.PredictedNextDate, s.IsActive, s.IsLinked);

}
