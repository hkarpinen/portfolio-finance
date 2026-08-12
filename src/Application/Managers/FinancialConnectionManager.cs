using Finance.Application.Commands;
using Finance.Application.Dtos;
using Finance.Application.Mappers;
using Finance.Application.Ports;
using Finance.Application.Queries;
using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ReadModels;
using Finance.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Finance.Application.Managers;

internal sealed class FinancialConnectionManager : IFinancialConnectionManager
{
    private readonly IBankDataProvider _api;
    private readonly IFinancialConnectionRepository _repo;
    private readonly IFinancialConnectionQuery _connectionQuery;
    private readonly IConnectionTokenProtector _tokenProtector;
    private readonly IChargeRepository _expenseRepository;
    private readonly IChargePaymentRepository _expensePaymentRepository;
    private readonly IIncomeSourceRepository _incomeRepository;
    private readonly IBankSyncManager _syncService;
    private readonly ILogger<FinancialConnectionManager> _logger;

    public FinancialConnectionManager(
        IBankDataProvider api,
        IFinancialConnectionRepository repo,
        IFinancialConnectionQuery connectionQuery,
        IConnectionTokenProtector tokenProtector,
        IChargeRepository expenseRepository,
        IChargePaymentRepository expensePaymentRepository,
        IIncomeSourceRepository incomeRepository,
        IBankSyncManager syncService,
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

    public async Task<LinkTokenDto> CreateLinkTokenAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var result = await _api.CreateLinkTokenAsync(userId, cancellationToken);
        return FinancialConnectionMapper.ToLinkToken(result.LinkToken, result.Expiration);
    }

    public async Task<ConnectionDto> ExchangePublicTokenAsync(
        Guid userId, LinkConnectionCommand request, CancellationToken cancellationToken = default)
    {
        var exchange = await _api.ExchangePublicTokenAsync(request.PublicToken, cancellationToken);

        // Idempotency: re-linking the same institution updates in place rather than creating a duplicate.
        // Plaid issues a new access_token on every exchange, so we always store the freshest one.
        var existing = await _repo.GetConnectionByExternalIdAsync(exchange.ItemId, cancellationToken);
        var encrypted = _tokenProtector.Protect(exchange.AccessToken);
        FinancialConnection connection;

        if (existing is not null)
        {
            // Auto-created charges/income must go BEFORE the connection does, since they are found through
            // its suggestions.
            var oldSuggestions = await _connectionQuery.ListSuggestionsForConnectionAsync(existing.Id, cancellationToken);
            foreach (var suggestion in oldSuggestions.Where(s => s.IsLinked && s.LinkedEntityId.HasValue))
            {
                if (suggestion.LinkedEntityType == LinkedEntityType.Charge)
                {
                    var expense = await _expenseRepository.GetByIdAsync(
                        ChargeId.Create(suggestion.LinkedEntityId!.Value), cancellationToken);
                    if (expense is not null) await _expenseRepository.RemoveAsync(expense, cancellationToken);
                }
                else if (suggestion.LinkedEntityType == LinkedEntityType.IncomeSource)
                {
                    var income = await _incomeRepository.GetByIdAsync(
                        IncomeId.Create(suggestion.LinkedEntityId!.Value), cancellationToken);
                    if (income is not null) await _incomeRepository.RemoveAsync(income, cancellationToken);
                }
            }

            // Re-link is modelled as remove + add because FinancialConnection has no public setter for
            // EncryptedAccessToken (intentionally invariant-protected).
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

        return FinancialConnectionMapper.ToResponse(connection, persistedAccounts);
    }

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

        var suggestions = await _connectionQuery.ListSuggestionsForConnectionAsync(connection.Id, cancellationToken);
        foreach (var suggestion in suggestions.Where(s => s.IsLinked && s.LinkedEntityId.HasValue))
        {
            if (suggestion.LinkedEntityType == LinkedEntityType.Charge)
            {
                var expense = await _expenseRepository.GetByIdAsync(
                    ChargeId.Create(suggestion.LinkedEntityId!.Value), cancellationToken);
                if (expense is not null)
                    await _expenseRepository.RemoveAsync(expense, cancellationToken);
            }
            else if (suggestion.LinkedEntityType == LinkedEntityType.IncomeSource)
            {
                var income = await _incomeRepository.GetByIdAsync(
                    IncomeId.Create(suggestion.LinkedEntityId!.Value), cancellationToken);
                if (income is not null)
                    await _incomeRepository.RemoveAsync(income, cancellationToken);
            }
        }

        // Removing the connection cascades to accounts, transactions and suggestions at the DB level.
        connection.MarkRevoked();
        await _repo.RemoveConnectionAsync(connection, cancellationToken);
        await _repo.CommitAsync(cancellationToken);
    }

    public async Task<SyncConnectionDto> SyncAsync(
        Guid userId, SyncConnectionCommand request, CancellationToken cancellationToken = default)
    {
        var connection = await _repo.GetConnectionAsync(
            FinancialConnectionId.Create(request.ConnectionId), cancellationToken)
            ?? throw new KeyNotFoundException("Financial connection not found.");

        if (connection.UserId.Value != userId)
            throw new UnauthorizedAccessException("Access denied.");

        var (added, modified, removed, hasMore) = await _syncService.SyncConnectionAsync(connection, cancellationToken);
        return FinancialConnectionMapper.ToSyncResult(
            connection.Id.Value, added, modified, removed, hasMore, DateTime.UtcNow);
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

    public async Task<RecurringSuggestionListDto> RefreshSuggestionsAsync(
        Guid userId, RefreshSuggestionsCommand request, CancellationToken ct = default)
    {
        var connection = await _repo.GetConnectionAsync(
            FinancialConnectionId.Create(request.ConnectionId), ct)
            ?? throw new KeyNotFoundException("Financial connection not found.");
        if (connection.UserId.Value != userId)
            throw new UnauthorizedAccessException("Access denied.");

        await _syncService.RefreshSuggestionsAsync(connection, ct);

        var suggestions = await _connectionQuery.ListSuggestionsForUserAsync(UserId.Create(userId), ct);
        return FinancialConnectionMapper.ToSuggestionList(suggestions);
    }

    public async Task<AcceptSuggestionDto> AcceptSuggestionAsync(
        Guid userId, AcceptSuggestionCommand request, CancellationToken ct = default)
    {
        var suggestion = await _repo.GetSuggestionAsync(request.SuggestionId, ct)
            ?? throw new KeyNotFoundException("Recurring suggestion not found.");

        if (suggestion.UserId.Value != userId)
            throw new UnauthorizedAccessException("Access denied.");

        if (suggestion.IsLinked && suggestion.LinkedEntityId.HasValue && !string.IsNullOrEmpty(suggestion.LinkedEntityType))
            return FinancialConnectionMapper.ToAccepted(suggestion.Id, suggestion.LinkedEntityId.Value, suggestion.LinkedEntityType);

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
            suggestion.MarkLinked(income.Id.Value, LinkedEntityType.IncomeSource);
            await _repo.SaveSuggestionAsync(suggestion, ct);
            await _repo.CommitAsync(ct);
            return FinancialConnectionMapper.ToAccepted(suggestion.Id, income.Id.Value, LinkedEntityType.IncomeSource);
        }
        else
        {
            var nextDue = suggestion.PredictedNextDate ?? suggestion.LastDate.AddDays(1);
            if (nextDue < DateTime.UtcNow.Date)
                nextDue = DateTime.UtcNow.Date.AddDays(1);

            var expense = Charge.Create(
                UserId.Create(userId), sourceName, suggestion.AverageAmount,
                ChargeCategory.Other, nextDue, schedule);
            await _expenseRepository.AddAsync(expense, ct);
            suggestion.MarkLinked(expense.Id.Value, LinkedEntityType.Charge);
            await _repo.SaveSuggestionAsync(suggestion, ct);
            await _repo.CommitAsync(ct);
            return FinancialConnectionMapper.ToAccepted(suggestion.Id, expense.Id.Value, LinkedEntityType.Charge);
        }
    }

    public async Task<AcceptSuggestionDto> AcceptBankSyncSuggestionAsync(
        Guid userId, AcceptBankSyncSuggestionCommand request, CancellationToken ct = default)
    {
        var suggestion = await _repo.GetBankSyncSuggestionAsync(request.SuggestionId, ct)
            ?? throw new KeyNotFoundException("Bank sync suggestion not found.");

        if (suggestion.UserId.Value != userId)
            throw new UnauthorizedAccessException("Access denied.");

        if (suggestion.IsLinked && suggestion.LinkedEntityId.HasValue && !string.IsNullOrEmpty(suggestion.LinkedEntityType))
            return FinancialConnectionMapper.ToAccepted(suggestion.Id, suggestion.LinkedEntityId.Value, suggestion.LinkedEntityType);

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
            suggestion.MarkLinked(income.Id.Value, LinkedEntityType.IncomeSource);
            await _repo.SaveBankSyncSuggestionAsync(suggestion, ct);
            await _repo.CommitAsync(ct);
            return FinancialConnectionMapper.ToAccepted(suggestion.Id, income.Id.Value, LinkedEntityType.IncomeSource);
        }
        else
        {
            var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, suggestion.TransactionDate);
            var nextDue = suggestion.TransactionDate.AddMonths(1);
            if (nextDue < DateTime.UtcNow.Date)
                nextDue = DateTime.UtcNow.Date.AddDays(1);

            Charge expense;
            if (request.GroupId.HasValue)
            {
                expense = Charge.CreateGroup(
                    GroupId.Create(request.GroupId.Value),
                    UserId.Create(userId),
                    displayName, amount, ChargeCategory.Other, nextDue, schedule);
                expense.Activate();
            }
            else
            {
                expense = Charge.Create(
                    UserId.Create(userId), displayName, amount,
                    ChargeCategory.Other, nextDue, schedule);
            }

            var payment = ChargePayment.Create(
                expense.Id, expense.UserId,
                suggestion.TransactionDate, suggestion.ExternalTransactionId);
            await _expenseRepository.AddAsync(expense, ct);
            await _expensePaymentRepository.AddAsync(payment, ct);
            suggestion.MarkLinked(expense.Id.Value, LinkedEntityType.Charge);
            await _repo.SaveBankSyncSuggestionAsync(suggestion, ct);
            await _repo.CommitAsync(ct);
            return FinancialConnectionMapper.ToAccepted(suggestion.Id, expense.Id.Value, LinkedEntityType.Charge);
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


}
