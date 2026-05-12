using Finance.Application.Ports;
using Finance.Application.Queries;
using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.Engines;
using Finance.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Finance.Application.Services;

internal sealed class BankSyncService : IBankSyncService
{
    private readonly IBankDataProvider _api;
    private readonly IFinancialConnectionRepository _repo;
    private readonly IFinancialConnectionQuery _connectionQuery;
    private readonly IConnectionTokenProtector _tokenProtector;
    private readonly IExpenseRepository _expenseRepository;
    private readonly IExpensePaymentRepository _expensePaymentRepository;
    private readonly IIncomeSourceRepository _incomeRepository;
    private readonly IExpenseQuery _expenseQuery;
    private readonly IIncomeQuery _incomeQuery;
    private readonly ILogger<BankSyncService> _logger;

    public BankSyncService(
        IBankDataProvider api,
        IFinancialConnectionRepository repo,
        IFinancialConnectionQuery connectionQuery,
        IConnectionTokenProtector tokenProtector,
        IExpenseRepository expenseRepository,
        IExpensePaymentRepository expensePaymentRepository,
        IIncomeSourceRepository incomeRepository,
        IExpenseQuery expenseQuery,
        IIncomeQuery incomeQuery,
        ILogger<BankSyncService> logger)
    {
        _api = api;
        _repo = repo;
        _connectionQuery = connectionQuery;
        _tokenProtector = tokenProtector;
        _expenseRepository = expenseRepository;
        _expensePaymentRepository = expensePaymentRepository;
        _incomeRepository = incomeRepository;
        _expenseQuery = expenseQuery;
        _incomeQuery = incomeQuery;
        _logger = logger;
    }

    // ── Transaction sync ─────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<(int Added, int Modified, int Removed, bool HasMore)> SyncConnectionAsync(
        FinancialConnection connection, CancellationToken cancellationToken = default)
    {
        var accessToken = _tokenProtector.Unprotect(connection.EncryptedAccessToken);
        int totalAdded = 0, totalModified = 0, totalRemoved = 0;
        bool hasMore;

        // Outflow transactions added this sync — candidates for auto-pay matching.
        var addedOutflows = new List<(Guid AccountId, decimal Amount, DateTime Date)>();

        // Cache account lookups to avoid hitting the DB once per transaction.
        var accountsByExternalId = (await _connectionQuery.ListAccountsForConnectionAsync(connection.Id, cancellationToken))
            .ToDictionary(a => a.ExternalAccountId, a => a);

        do
        {
            var page = await _api.SyncTransactionsAsync(accessToken, connection.Cursor, cancellationToken);

            var modifiedIds = page.Modified.Select(m => m.TransactionId);
            var removedIds = page.Removed;
            var existingLookups = await _repo.LookupTransactionsByExternalIdsAsync(
                modifiedIds.Concat(removedIds).Distinct(), cancellationToken);

            foreach (var dto in page.Added)
            {
                if (!accountsByExternalId.TryGetValue(dto.AccountId, out var account))
                {
                    account = await EnsureAccountAsync(connection, accessToken, dto.AccountId, accountsByExternalId, cancellationToken);
                    if (account is null) continue;
                }

                var txn = FinancialTransaction.Create(
                    connection.Id, account.Id, connection.UserId,
                    dto.TransactionId,
                    Money.Create(Math.Abs(dto.Amount), dto.Currency),
                    dto.Date, dto.AuthorizedDate, dto.Name, dto.MerchantName,
                    dto.PrimaryCategory, dto.DetailedCategory, dto.Pending);

                await _repo.AddTransactionAsync(txn, cancellationToken);
                totalAdded++;

                if (!dto.Pending)
                {
                    var existingSuggestion = await _repo.GetBankSyncSuggestionByExternalTransactionIdAsync(
                        dto.TransactionId, cancellationToken);
                    if (existingSuggestion is null)
                    {
                        var direction = BankSyncMatchingEngine.ResolveDirection(dto.Amount);
                        var suggestion = BankSyncSuggestion.Create(
                            connection.Id, connection.UserId,
                            dto.TransactionId,
                            dto.Name, dto.MerchantName,
                            Math.Abs(dto.Amount), dto.Currency,
                            direction, dto.Date);
                        await _repo.AddBankSyncSuggestionAsync(suggestion, cancellationToken);
                    }
                }

                if (dto.Amount > 0 && !dto.Pending)
                    addedOutflows.Add((account.Id, dto.Amount, dto.Date));
            }

            foreach (var dto in page.Modified)
            {
                if (!existingLookups.TryGetValue(dto.TransactionId, out var existing)) continue;
                existing.ApplyUpdate(
                    Money.Create(Math.Abs(dto.Amount), dto.Currency),
                    dto.Date, dto.AuthorizedDate, dto.Name, dto.MerchantName,
                    dto.PrimaryCategory, dto.DetailedCategory, dto.Pending);
                totalModified++;
            }

            foreach (var removedId in page.Removed)
            {
                await _repo.RemoveTransactionByExternalIdAsync(removedId, cancellationToken);
                totalRemoved++;
            }

            // Advance cursor INSIDE the same commit as the row writes — crash-safe.
            connection.AdvanceCursor(page.NextCursor);
            await _repo.SaveConnectionAsync(connection, cancellationToken);
            await _repo.CommitAsync(cancellationToken);

            hasMore = page.HasMore;
        } while (hasMore);

        _logger.LogInformation(
            "Synced connection {ConnectionId}: +{Added} ~{Modified} -{Removed}",
            connection.ExternalId, totalAdded, totalModified, totalRemoved);

        if (totalAdded > 0 || totalModified > 0)
        {
            try
            {
                await RefreshSuggestionsAsync(connection, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Auto recurring-suggestion detection failed for connection {ConnectionId}; can be refreshed manually.",
                    connection.ExternalId);
            }
        }

        if (addedOutflows.Count > 0)
        {
            try
            {
                await TryAutoPayExpensesAsync(connection.Id.Value, addedOutflows, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Auto-pay matching failed for connection {ConnectionId}; expenses can still be marked paid manually.",
                    connection.ExternalId);
            }
        }

        return (totalAdded, totalModified, totalRemoved, false);
    }

    // ── Recurring suggestion refresh ─────────────────────────────────────────

    /// <inheritdoc />
    public async Task RefreshSuggestionsAsync(
        FinancialConnection connection, CancellationToken ct = default)
    {
        var accessToken = _tokenProtector.Unprotect(connection.EncryptedAccessToken);
        var result = await _api.GetRecurringTransactionsAsync(accessToken, ct);

        var accountsByExternalId = (await _connectionQuery.ListAccountsForConnectionAsync(connection.Id, ct))
            .ToDictionary(a => a.ExternalAccountId, a => a);

        await UpsertSuggestionsAsync(connection, result.Inflow,  RecurringFlowDirection.Inflow,  accountsByExternalId, ct);
        await UpsertSuggestionsAsync(connection, result.Outflow, RecurringFlowDirection.Outflow, accountsByExternalId, ct);
        await _repo.CommitAsync(ct);
    }

    // ── Upsert recurring suggestions ─────────────────────────────────────────

    private async Task UpsertSuggestionsAsync(
        FinancialConnection connection,
        IReadOnlyList<RecurringStreamDto> streams,
        RecurringFlowDirection direction,
        IReadOnlyDictionary<string, FinancialAccount> accountsByExternalId,
        CancellationToken ct)
    {
        foreach (var dto in streams)
        {
            if (!accountsByExternalId.TryGetValue(dto.AccountId, out var account)) continue;

            var freq = dto.Frequency;
            var avg  = Money.Create(Math.Abs(dto.AverageAmount), dto.Currency);
            var last = Money.Create(Math.Abs(dto.LastAmount),    dto.Currency);

            var existing = await _repo.GetSuggestionByExternalIdAsync(dto.StreamId, ct);
            if (existing is null)
            {
                var suggestion = RecurringSuggestion.Create(
                    connection.Id, account.Id, connection.UserId, dto.StreamId, direction,
                    dto.Description, dto.MerchantName, freq, avg, last,
                    dto.FirstDate, dto.LastDate, dto.PredictedNextDate, dto.IsActive);
                await _repo.AddSuggestionAsync(suggestion, ct);
                await AutoLinkSuggestionAsync(connection.UserId.Value, suggestion, ct);
            }
            else
            {
                existing.ApplyUpdate(
                    dto.Description, dto.MerchantName, freq, avg, last,
                    dto.FirstDate, dto.LastDate, dto.PredictedNextDate, dto.IsActive);
                await _repo.SaveSuggestionAsync(existing, ct);
            }
        }
    }

    // ── Auto-link new suggestions to existing income / expense records ────────

    private async Task AutoLinkSuggestionAsync(Guid userId, RecurringSuggestion suggestion, CancellationToken ct)
    {
        if (suggestion.IsLinked) return;

        var sourceName = !string.IsNullOrWhiteSpace(suggestion.MerchantName) ? suggestion.MerchantName
            : !string.IsNullOrWhiteSpace(suggestion.Description) ? suggestion.Description
            : "Unknown";
        var schedule = RecurrenceSchedule.Create(suggestion.Frequency, suggestion.FirstDate);
        var uid = UserId.Create(userId);

        if (suggestion.Direction == RecurringFlowDirection.Inflow)
        {
            if (await _incomeQuery.ExistsForUserAsync(uid, sourceName, suggestion.AverageAmount.Amount, ct))
            {
                _logger.LogDebug(
                    "Skipping auto-link for inflow suggestion {SuggestionId} — matching IncomeSource already exists.",
                    suggestion.Id);
                return;
            }

            var income = IncomeSource.Create(
                uid, suggestion.AverageAmount, sourceName, schedule,
                paymentFrequency: suggestion.Frequency,
                lastPaymentDate: suggestion.LastDate);
            await _incomeRepository.AddAsync(income, ct);
            suggestion.MarkLinked(income.Id.Value, "IncomeSource");
            _logger.LogInformation(
                "Auto-linked suggestion {SuggestionId} → IncomeSource {IncomeId} ({Source})",
                suggestion.Id, income.Id.Value, sourceName);
        }
        else
        {
            if (await _expenseQuery.ExistsForUserAsync(uid, sourceName, suggestion.AverageAmount.Amount, ct))
            {
                _logger.LogDebug(
                    "Skipping auto-link for outflow suggestion {SuggestionId} — matching Expense already exists.",
                    suggestion.Id);
                return;
            }

            var nextDue = suggestion.PredictedNextDate ?? suggestion.LastDate.AddDays(1);
            if (nextDue < DateTime.UtcNow.Date)
                nextDue = DateTime.UtcNow.Date.AddDays(1);

            var expense = Expense.Create(uid, sourceName, suggestion.AverageAmount, ExpenseCategory.Other, nextDue, schedule);
            await _expenseRepository.AddAsync(expense, ct);
            suggestion.MarkLinked(expense.Id.Value, "Expense");
            _logger.LogInformation(
                "Auto-linked suggestion {SuggestionId} → Expense {ExpenseId} ({Source})",
                suggestion.Id, expense.Id.Value, sourceName);
        }
    }

    // ── Auto-pay matching ─────────────────────────────────────────────────────

    private async Task TryAutoPayExpensesAsync(
        Guid connectionId,
        IReadOnlyList<(Guid AccountId, decimal Amount, DateTime Date)> outflows,
        CancellationToken cancellationToken)
    {
        var connectionIdVo = FinancialConnectionId.Create(connectionId);
        var suggestions = await _connectionQuery.ListSuggestionsForConnectionAsync(connectionIdVo, cancellationToken);
        var expenseSuggestions = suggestions
            .Where(s => s.IsLinked && s.LinkedEntityType == "Expense" && s.LinkedEntityId.HasValue)
            .ToList();

        if (expenseSuggestions.Count == 0) return;

        foreach (var (accountId, amount, date) in outflows)
        {
            var match = expenseSuggestions.FirstOrDefault(s => BankSyncMatchingEngine.IsMatch(s, accountId, amount));
            if (match is null) continue;

            var expenseId = ExpenseId.Create(match.LinkedEntityId!.Value);
            var expense = await _expenseRepository.GetByIdAsync(expenseId, cancellationToken);
            if (expense is null) continue;

            var occurrenceDate = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            var existing = await _expensePaymentRepository.GetAsync(expenseId, occurrenceDate, cancellationToken);
            if (existing is not null) continue;

            var payment = ExpensePayment.Create(expense.Id, expense.UserId, occurrenceDate);
            await _expensePaymentRepository.AddAsync(payment, cancellationToken);

            _logger.LogInformation(
                "Auto-paid expense {ExpenseId} for occurrence {OccurrenceDate} via synced transaction (amount {Amount}).",
                expense.Id.Value, occurrenceDate.ToString("yyyy-MM-dd"), amount);
        }

        await _repo.CommitAsync(cancellationToken);
    }

    // ── Account helpers ───────────────────────────────────────────────────────

    private async Task<FinancialAccount?> EnsureAccountAsync(
        FinancialConnection connection,
        string accessToken,
        string externalAccountId,
        Dictionary<string, FinancialAccount> cache,
        CancellationToken cancellationToken)
    {
        var existing = await _repo.GetAccountByExternalIdAsync(connection.Id, externalAccountId, cancellationToken);
        if (existing is not null)
        {
            cache[externalAccountId] = existing;
            return existing;
        }

        var accountsResult = await _api.GetAccountsAsync(accessToken, cancellationToken);
        foreach (var dto in accountsResult.Accounts)
        {
            if (cache.ContainsKey(dto.AccountId)) continue;
            var account = FinancialAccount.Create(
                connection.Id, connection.UserId, dto.AccountId, dto.Name, dto.OfficialName, dto.Mask,
                dto.Type, dto.Subtype, dto.CurrencyCode, dto.CurrentBalance, dto.AvailableBalance);
            await _repo.AddAccountAsync(account, cancellationToken);
            cache[dto.AccountId] = account;
        }

        await _repo.CommitAsync(cancellationToken);
        return cache.GetValueOrDefault(externalAccountId);
    }
}
