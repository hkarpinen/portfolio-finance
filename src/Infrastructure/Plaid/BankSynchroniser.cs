using Finance.Application.Managers;
using Finance.Application.Ports;
using Finance.Application.Queries;
using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.Engines;
using Infrastructure.Plaid.Mirrors;
using Finance.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Plaid;

internal sealed class BankSynchroniser : IBankSynchroniser
{
    private readonly IBankDataProvider _api;
    private readonly IFinancialConnectionRepository _repo;
    private readonly IFinancialConnectionQuery _connectionQuery;
    private readonly IConnectionTokenProtector _tokenProtector;
    private readonly IExpenseRepository _expenseRepository;
    private readonly IIncomeSourceRepository _incomeRepository;
    private readonly IExpenseQuery _expenseQuery;
    private readonly IIncomeQuery _incomeQuery;
    private readonly IBankSyncMatchingEngine _matchingEngine;
    private readonly IReceiptRepository _receipts;
    private readonly IBookkeepingManager _bookkeeping;
    private readonly ILogger<BankSynchroniser> _logger;

    public BankSynchroniser(
        IBankDataProvider api,
        IFinancialConnectionRepository repo,
        IFinancialConnectionQuery connectionQuery,
        IConnectionTokenProtector tokenProtector,
        IExpenseRepository expenseRepository,
        IIncomeSourceRepository incomeRepository,
        IExpenseQuery expenseQuery,
        IIncomeQuery incomeQuery,
        IBankSyncMatchingEngine matchingEngine,
        IReceiptRepository receipts,
        IBookkeepingManager bookkeeping,
        ILogger<BankSynchroniser> logger)
    {
        _api = api;
        _repo = repo;
        _connectionQuery = connectionQuery;
        _tokenProtector = tokenProtector;
        _expenseRepository = expenseRepository;
        _incomeRepository = incomeRepository;
        _expenseQuery = expenseQuery;
        _incomeQuery = incomeQuery;
        _matchingEngine = matchingEngine;
        _receipts = receipts;
        _bookkeeping = bookkeeping;
        _logger = logger;
    }

    public async Task<(int Added, int Modified, int Removed, bool HasMore)> SyncConnectionAsync(
        FinancialConnection connection, CancellationToken cancellationToken = default)
    {
        var accessToken = _tokenProtector.Unprotect(connection.EncryptedAccessToken);

        // Before anything else: a transaction has nowhere to post until the account it came from
        // has a place in the owner's books.
        await PlaceAccountsInLedgerAsync(connection, cancellationToken);

        int totalAdded = 0, totalModified = 0, totalRemoved = 0;
        bool hasMore;

        // Outflow transactions added this sync — the candidates for auto-pay matching.
        var addedOutflows = new List<(Guid AccountId, decimal Amount, DateTime Date)>();

        // Cached so the loop below does not hit the DB once per transaction.
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

                // Signed as the provider reported it. Both call sites used to abs() here, which
                // made IsInflow permanently false and every deposit read as a payment.
                var txn = FinancialTransaction.Create(
                    connection.Id, account.Id, connection.UserId,
                    dto.TransactionId,
                    Money.Create(dto.Amount, dto.Currency),
                    dto.Date, dto.AuthorizedDate, dto.Name, dto.MerchantName,
                    dto.PrimaryCategory, dto.DetailedCategory, dto.Pending);

                await _repo.AddTransactionAsync(txn, cancellationToken);
                totalAdded++;

                // Pending is imported too, and reversed if the provider withdraws it. A charge you
                // have made is a charge you have made; waiting for it to settle would leave a
                // balance that disagrees with the one on your phone.
                await ImportAsync(connection, account, txn, cancellationToken);

                if (dto.Amount > 0 && !dto.Pending)
                    addedOutflows.Add((account.Id, dto.Amount, dto.Date));
            }

            foreach (var dto in page.Modified)
            {
                if (!existingLookups.TryGetValue(dto.TransactionId, out var existing)) continue;
                existing.ApplyUpdate(
                    Money.Create(dto.Amount, dto.Currency),
                    dto.Date, dto.AuthorizedDate, dto.Name, dto.MerchantName,
                    dto.PrimaryCategory, dto.DetailedCategory, dto.Pending);
                totalModified++;
            }

            foreach (var removedId in page.Removed)
            {
                // A withdrawn transaction is usually a pending one being replaced by the settled
                // version under a new id. Whatever it became has to come off the books first, or
                // the settled copy lands beside it and the month is counted twice.
                if (existingLookups.TryGetValue(removedId, out var withdrawn))
                    await UnimportAsync(withdrawn, cancellationToken);

                await _repo.RemoveTransactionByExternalIdAsync(removedId, cancellationToken);
                totalRemoved++;
            }

            // Advance the cursor INSIDE the same commit as the row writes, so a crash cannot skip a page.
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

    /// <summary>
    /// Brings one transaction into the books: money out becomes an expense already paid from the
    /// account it left, money in becomes a receipt into the account it landed in.
    ///
    /// Nothing is imported twice — the mirror remembers what it became — and nothing is imported
    /// from an account that has no place in the ledger yet, because it would have nowhere to post.
    /// </summary>
    private async Task ImportAsync(
        FinancialConnection connection, FinancialAccount account, FinancialTransaction txn,
        CancellationToken ct)
    {
        if (txn.IsImported) return;
        if (account.LedgerAccountId is not { } ledgerAccountId)
        {
            _logger.LogWarning(
                "Transaction {TransactionId} not imported — account {AccountId} has no place in the ledger.",
                txn.ExternalTransactionId, account.Id);
            return;
        }

        var amount = Money.Create(Math.Abs(txn.Amount.Amount), txn.Amount.Currency);
        var name = string.IsNullOrWhiteSpace(txn.MerchantName) ? txn.Name : txn.MerchantName;

        if (txn.IsInflow)
        {
            var receipt = Receipt.Record(connection.UserId, ledgerAccountId, name, amount, txn.Date);
            await _receipts.AddAsync(receipt, ct);
            txn.ImportedAs(receipt.Id.Value, LinkedEntityType.Receipt);
            return;
        }

        var expense = Expense.CreateOwn(
            connection.UserId, name, amount,
            BankCategories.ToExpenseCategory(txn.PrimaryCategory), txn.Date,
            fundingAccountId: ledgerAccountId);

        // A bank transaction is a cost that has already been paid — both facts are known at once,
        // so both are recorded now rather than leaving it sitting in payables forever.
        expense.RecordPersonalPayment(ledgerAccountId, txn.Date);

        await _expenseRepository.AddAsync(expense, ct);
        txn.ImportedAs(expense.Id.Value, LinkedEntityType.Expense);
    }

    /// <summary>Takes back whatever a transaction became, when the provider takes the transaction
    /// back. Voided rather than deleted, so the books can unwind what they were told.</summary>
    private async Task UnimportAsync(FinancialTransaction txn, CancellationToken ct)
    {
        if (txn.LinkedEntityId is not { } entityId) return;

        if (txn.LinkedEntityType == LinkedEntityType.Receipt)
        {
            var receipt = await _receipts.GetByIdAsync(ReceiptId.Create(entityId), ct);
            receipt?.Void();
            if (receipt is not null) await _receipts.UpdateAsync(receipt, ct);
            return;
        }

        var expense = await _expenseRepository.GetByIdAsync(ExpenseId.Create(entityId), ct);
        if (expense is null) return;

        expense.TryDeactivate();
        await _expenseRepository.UpdateAsync(expense, ct);
    }

    public async Task PlaceAccountsInLedgerAsync(FinancialConnection connection, CancellationToken ct = default)
    {
        var accounts = await _connectionQuery.ListAccountsForConnectionAsync(connection.Id, ct);

        foreach (var account in accounts)
        {
            if (account.LedgerAccountId is not null) continue;

            var ledgerAccountId = await _bookkeeping.OpenBankAccountAsync(new OpenBankAccountCommand(
                CallerUserId: connection.UserId.Value,
                Name: account.Name,
                Currency: account.CurrencyCode,
                IsCredit: account.IsCredit,
                // A card reports what you owe as a positive number and so does a current account
                // report what you hold, so the figure carries in as given and its side comes from
                // the account's kind.
                Balance: account.CurrentBalance ?? 0m,
                AsOf: DateTime.UtcNow.Date), ct);

            account.PlaceInLedger(ledgerAccountId);
            await _repo.SaveAccountAsync(account, ct);

            _logger.LogInformation(
                "Placed connected account {AccountId} in the ledger as {LedgerAccountId}.",
                account.Id, ledgerAccountId);
        }

        await _repo.CommitAsync(ct);
    }

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

    /// <summary>
    /// Turns what the policy proposes into a document, unless the person already has one like it.
    /// The decision is SuggestionLinkPolicy's; what is left here is the exists-check and the write,
    /// which are the only parts that need the database.
    /// </summary>
    private async Task AutoLinkSuggestionAsync(Guid userId, RecurringSuggestion suggestion, CancellationToken ct)
    {
        var proposal = SuggestionLinkPolicy.Propose(suggestion, DateTime.UtcNow.Date);
        if (proposal.Document == SuggestedDocument.None) return;

        var uid = UserId.Create(userId);

        if (proposal.Document == SuggestedDocument.IncomeSource)
        {
            if (await _incomeQuery.ExistsForUserAsync(uid, proposal.SourceName, proposal.Amount.Amount, ct))
            {
                _logger.LogDebug(
                    "Skipping auto-link for inflow suggestion {SuggestionId} — matching IncomeSource already exists.",
                    suggestion.Id);
                return;
            }

            var income = IncomeSource.Create(
                uid, proposal.Amount, proposal.SourceName,
                RecurrenceSchedule.Create(suggestion.Frequency, suggestion.FirstDate),
                paymentFrequency: suggestion.Frequency,
                lastPaymentDate: proposal.NextDue);
            await _incomeRepository.AddAsync(income, ct);
            suggestion.MarkLinked(income.Id.Value, LinkedEntityType.IncomeSource);
            _logger.LogInformation(
                "Auto-linked suggestion {SuggestionId} → IncomeSource {IncomeId} ({Source})",
                suggestion.Id, income.Id.Value, proposal.SourceName);
            return;
        }

        if (await _expenseQuery.ExistsForUserAsync(uid, proposal.SourceName, proposal.Amount.Amount, ct))
        {
            _logger.LogDebug(
                "Skipping auto-link for outflow suggestion {SuggestionId} — matching Expense already exists.",
                suggestion.Id);
            return;
        }

        var expense = Expense.CreateOwn(
            uid, proposal.SourceName, proposal.Amount, ExpenseCategory.Other, proposal.NextDue);
        await _expenseRepository.AddAsync(expense, ct);
        suggestion.MarkLinked(expense.Id.Value, LinkedEntityType.Expense);
        _logger.LogInformation(
            "Auto-linked suggestion {SuggestionId} → Expense {ExpenseId} ({Source})",
            suggestion.Id, expense.Id.Value, proposal.SourceName);
    }

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
            var account = FinancialConnectionMapper.ToAccount(connection, dto);
            await _repo.AddAccountAsync(account, cancellationToken);
            cache[dto.AccountId] = account;
        }

        await _repo.CommitAsync(cancellationToken);
        return cache.GetValueOrDefault(externalAccountId);
    }
}
