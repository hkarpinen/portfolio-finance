using Finance.Application.Commands;
using Finance.Application.Dtos;
using Finance.Application.Ports;
using Finance.Application.Queries;
using Finance.Application.Repositories;
using Finance.Application.Mappers;
using Finance.Domain.Aggregates;
using Finance.Domain.Engines;
using Finance.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
namespace Finance.Application.Managers;

internal sealed class ExpenseManager : IExpenseManager
{
    private readonly IExpenseRepository _repository;
    private readonly IShareRepository _splitRepository;
    private readonly IFinancialConnectionRepository _connectionRepository;
    private readonly ILedgerQuery _ledgerQuery;
    private readonly IRecurringExpenseManager _schedules;
    private readonly ILogger<ExpenseManager> _logger;

    public ExpenseManager(
        IExpenseRepository repository,
        IShareRepository splitRepository,
        IFinancialConnectionRepository connectionRepository,
        ILedgerQuery ledgerQuery,
        IRecurringExpenseManager schedules,
        ILogger<ExpenseManager> logger)
    {
        _repository = repository;
        _splitRepository = splitRepository;
        _connectionRepository = connectionRepository;
        _ledgerQuery = ledgerQuery;
        _schedules = schedules;
        _logger = logger;
    }

    public async Task<ExpenseResponseDto> CreateAsync(CreateExpenseCommand request, CancellationToken cancellationToken = default)
    {
        var category = ExpenseCategories.Parse(request.Category);

        var recurrence = RecurrenceSchedule.ParseOrNone(
            request.RecurrenceFrequency, request.RecurrenceStartDate ?? request.DueDate, request.RecurrenceEndDate);

        // A repeating personal cost is an agreement, same as a repeating house bill. Only the
        // one-off is an expense somebody typed straight in.
        if (recurrence is not null)
        {
            var schedule = await _schedules.CreateAsync(new CreateRecurringExpenseCommand(
                GroupId: null,
                CallerUserId: request.CallerUserId,
                Title: request.Title,
                Amount: request.Amount,
                Currency: request.Currency,
                Category: category,
                Frequency: recurrence.Frequency,
                AnchorDate: recurrence.StartDate,
                EndDate: recurrence.EndDate,
                Description: request.Description), cancellationToken);

            var first = await _schedules.MaterialiseAsync(schedule.RecurringExpenseId, recurrence.StartDate, cancellationToken)
                ?? throw new InvalidOperationException("The schedule was created but its first expense was not.");
            return ExpenseMapper.ToResponse(first);
        }

        var expense = Expense.CreateOwn(
            UserId.Create(request.CallerUserId),
            request.Title,
            Money.Create(request.Amount, request.Currency),
            category,
            request.DueDate,
            request.Description,
            request.FundingAccountId);

        await _repository.AddAsync(expense, cancellationToken);
        await _repository.CommitAsync(cancellationToken);
        return ExpenseMapper.ToResponse(expense);
    }

    public async Task<ExpenseResponseDto?> UpdateAsync(UpdateExpenseCommand request, CancellationToken cancellationToken = default)
    {
        var expense = await _repository.GetByIdAsync(ExpenseId.Create(request.ExpenseId), cancellationToken);
        if (expense is null) return null;

        // Owner check. Null (→ 404), never 403, so ids stay unprobeable.
        if (!expense.IsOwnedBy(request.CallerUserId)) return null;

        var category = ExpenseCategories.Parse(request.Category);

        expense.Update(
            request.Title,
            Money.Create(request.Amount, request.Currency),
            category,
            request.DueDate,
            request.Description);

        await _repository.UpdateAsync(expense, cancellationToken);
        await _repository.CommitAsync(cancellationToken);
        return ExpenseMapper.ToResponse(expense);
    }

    public async Task<ExpenseResponseDto?> DeleteAsync(DeleteExpenseCommand request, CancellationToken cancellationToken = default)
    {
        var expense = await _repository.GetByIdAsync(ExpenseId.Create(request.ExpenseId), cancellationToken);
        if (expense is null) return null;

        // Owner check — deletion must not be reachable by id alone.
        if (!expense.IsOwnedBy(request.CallerUserId)) return null;

        if (expense.TryDeactivate())
            await _repository.UpdateAsync(expense, cancellationToken);

        var suggestion = await _connectionRepository.GetSuggestionByLinkedEntityIdAsync(request.ExpenseId, cancellationToken);
        if (suggestion is not null)
        {
            suggestion.Unlink();
            await _connectionRepository.SaveSuggestionAsync(suggestion, cancellationToken);
        }

        await _repository.CommitAsync(cancellationToken);
        return ExpenseMapper.ToResponse(expense);
    }

    public async Task<ExpenseResponseDto> CreateGroupExpenseAsync(CreateGroupExpenseCommand request, CancellationToken cancellationToken = default)
    {
        // A group expense ALWAYS has a payer; defaulting it here makes PayerUserId a real
        // invariant, so readers never need a `?? CreatedBy` fallback.
        var payerUserId = request.PayerUserId ?? request.CallerUserId;
        var recurrence = RecurrenceSchedule.CreateOrNone(
            request.RecurrenceFrequency, request.RecurrenceStartDate ?? request.DueDate, request.RecurrenceEndDate);

        // A cost that comes round again is an agreement, and the bill for one month is generated
        // from it. Creating a repeating Expense instead would put the cadence back on the document
        // and every month would be derived from it again.
        var expense = recurrence is null
            ? Expense.Create(
                AccountingEntity.Household(request.GroupId), UserId.Create(request.CallerUserId), request.Title,
                Money.Create(request.Amount, request.Currency), request.Category, request.DueDate,
                request.Description, payerUserId: payerUserId, fundingSource: request.FundingSource)
            : await OpenScheduleAndBillFirstOccurrenceAsync(request, recurrence, payerUserId, cancellationToken);

        if (expense.RecurringExpenseId is null)
            await _repository.AddAsync(expense, cancellationToken);

        // The share's actor is the identity userId (the PERSON), not the household
        // membership — one financial identity across all of that person's households.
        if (request.Shares is { Count: > 0 })
        {
            foreach (var split in request.Shares)
            {
                var entity = Share.Create(
                    expense,
                    UserId.Create(split.MemberUserId),
                    Money.Create(split.Amount, split.Currency));
                await _splitRepository.AddAsync(entity, cancellationToken);
            }
        }

        await _repository.CommitAsync(cancellationToken);
        return ExpenseMapper.ToResponse(expense);
    }

    /// <summary>
    /// Opens the agreement and bills the occurrence the caller was entering, so they get an expense
    /// back exactly as before — the schedule sits behind it, and every later month comes from it.
    /// </summary>
    private async Task<Expense> OpenScheduleAndBillFirstOccurrenceAsync(
        CreateGroupExpenseCommand request, RecurrenceSchedule recurrence, Guid payerUserId, CancellationToken ct)
    {
        var schedule = await _schedules.CreateAsync(new CreateRecurringExpenseCommand(
            GroupId: request.GroupId,
            CallerUserId: request.CallerUserId,
            Title: request.Title,
            Amount: request.Amount,
            Currency: request.Currency,
            Category: request.Category,
            Frequency: recurrence.Frequency,
            AnchorDate: recurrence.StartDate,
            EndDate: recurrence.EndDate,
            Description: request.Description,
            PayerUserId: payerUserId,
            FundingSource: request.FundingSource), ct);

        return await _schedules.MaterialiseAsync(schedule.RecurringExpenseId, recurrence.StartDate, ct)
            ?? throw new InvalidOperationException("The schedule was created but its first bill was not.");
    }

    public async Task<ExpenseResponseDto?> UpdateGroupExpenseAsync(UpdateGroupExpenseCommand request, CancellationToken cancellationToken = default)
    {
        var expense = await _repository.GetByIdAsync(ExpenseId.Create(request.ExpenseId), cancellationToken);
        if (expense is null) return null;

        // Shrinking below what is already split would strand the shares above the total. The
        // engine refuses to journalize that, and the refusal lands in a consumer — so it is caught
        // here, where the person who typed the number is still listening.
        var allocated = await _splitRepository.SumForExpenseAsync(expense.Id, null, cancellationToken);
        if (!ShareMath.CoversShares(request.Amount, allocated))
            throw new InvalidOperationException(
                $"Shares already total {allocated:0.##}; the expense cannot be less than that.");

        expense.Update(
            request.Title,
            Money.Create(request.Amount, request.Currency),
            request.Category,
            request.DueDate,
            request.Description,
            request.PayerUserId);

        await _repository.UpdateAsync(expense, cancellationToken);
        await _repository.CommitAsync(cancellationToken);
        return ExpenseMapper.ToResponse(expense);
    }

    public async Task<ExpenseResponseDto?> DeactivateGroupExpenseAsync(DeactivateExpenseCommand request, CancellationToken cancellationToken = default)
    {
        var expense = await _repository.GetByIdAsync(ExpenseId.Create(request.ExpenseId), cancellationToken);
        if (expense is null) return null;

        expense.Deactivate();
        await _repository.UpdateAsync(expense, cancellationToken);
        await _repository.CommitAsync(cancellationToken);
        return ExpenseMapper.ToResponse(expense);
    }

    public async Task<ShareDto> UpsertShareAsync(UpsertShareCommand request, CancellationToken cancellationToken = default)
    {
        var money = Money.Create(request.Amount, request.Currency);
        var expenseId = ExpenseId.Create(request.ExpenseId);

        var expense = await _repository.GetByIdAsync(expenseId, cancellationToken)
            ?? throw new KeyNotFoundException("Expense not found.");

        // Your own share, or you entered the bill. Being in the house lets you settle what you owe;
        // it does not let you re-cut how somebody else's bill was divided.
        if (request.MemberUserId != request.CallerUserId && !expense.IsManagedBy(request.CallerUserId))
            throw new UnauthorizedAccessException("Only the bill's owner can set another member's share.");

        var expenseTotal = expense.Amount.Amount;

        if (request.ShareId.HasValue)
        {
            var existing = await _splitRepository.GetByIdAsync(ShareId.Create(request.ShareId.Value), cancellationToken);
            if (existing is not null)
            {
                var others = await _splitRepository.SumForExpenseAsync(expenseId, existing.Id, cancellationToken);
                if (!ShareMath.Fits(others, money.Amount, expenseTotal))
                    throw new InvalidOperationException(
                        $"Shares would exceed the expense total of {expenseTotal:0.##}.");

                existing.Update(expense, money);
                await _splitRepository.UpdateAsync(existing, cancellationToken);
                // Touches the expense so this write goes through its version check — the reason
                // two people setting a share at once cannot both fit under the same total.
                expense.RecordShareChange();
                await _repository.UpdateAsync(expense, cancellationToken);
                await _splitRepository.CommitAsync(cancellationToken);
                return ExpenseMapper.ToShareResponse(existing);
            }
        }

        var duplicate = await _splitRepository.GetByExpenseAndUserAsync(
            expenseId,
            UserId.Create(request.MemberUserId),
            cancellationToken);

        if (duplicate is not null)
            throw new InvalidOperationException("A split for this member already exists on this expense.");

        var existingTotal = await _splitRepository.SumForExpenseAsync(expenseId, null, cancellationToken);
        if (!ShareMath.Fits(existingTotal, money.Amount, expenseTotal))
            throw new InvalidOperationException(
                $"Shares would exceed the expense total of {expenseTotal:0.##}.");

        // The group comes off the expense, not off the request — a caller naming a different one
        // was never a share of this bill.
        var split = Share.Create(expense, UserId.Create(request.MemberUserId), money);

        await _splitRepository.AddAsync(split, cancellationToken);
        expense.RecordShareChange();
        await _repository.UpdateAsync(expense, cancellationToken);
        await _splitRepository.CommitAsync(cancellationToken);
        return ExpenseMapper.ToShareResponse(split);
    }

    public async Task<ShareDto?> RemoveShareAsync(RemoveShareCommand request, CancellationToken cancellationToken = default)
    {
        var split = await _splitRepository.GetByIdAsync(ShareId.Create(request.ShareId), cancellationToken);
        if (split is null) return null;

        var expense = await _repository.GetByIdAsync(split.ExpenseId, cancellationToken)
            ?? throw new InvalidOperationException("Expense not found.");

        split.Remove(expense);
        await _splitRepository.RemoveAsync(split, cancellationToken);
        expense.RecordShareChange();
        await _repository.UpdateAsync(expense, cancellationToken);
        await _splitRepository.CommitAsync(cancellationToken);
        return ExpenseMapper.ToShareResponse(split);
    }

    public async Task AllocateEvenlyAsync(Guid expenseId, IReadOnlyList<Guid> userIds, CancellationToken cancellationToken = default)
    {
        var expense = await _repository.GetByIdAsync(ExpenseId.Create(expenseId), cancellationToken)
            ?? throw new InvalidOperationException("Expense not found.");

        if (expense.GroupId is null)
            throw new InvalidOperationException("Cannot split a personal expense.");

        if (userIds.Count == 0)
            throw new ArgumentException("At least one user is required.", nameof(userIds));

        // Round each share to 2 decimals with the last member absorbing the remainder so the shares
        // sum EXACTLY to the expense total (e.g. 100 / 3 → 33.33, 33.33, 33.34) — never overshooting.
        var shares = ShareMath.SplitEvenly(expense.Amount.Amount, userIds.Count);

        for (var i = 0; i < userIds.Count; i++)
        {
            var uid = UserId.Create(userIds[i]);
            var money = Money.Create(shares[i], expense.Amount.Currency);
            var existing = await _splitRepository.GetByExpenseAndUserAsync(expense.Id, uid, cancellationToken);
            if (existing is not null)
            {
                existing.Update(expense, money);
                await _splitRepository.UpdateAsync(existing, cancellationToken);
            }
            else
            {
                var split = Share.Create(expense, uid, money);
                await _splitRepository.AddAsync(split, cancellationToken);
            }
        }
        expense.RecordShareChange();
        await _repository.UpdateAsync(expense, cancellationToken);
        await _splitRepository.CommitAsync(cancellationToken);
    }

    public async Task<ShareDto?> AssignShareAsync(Guid groupId, Guid expenseId, Guid userId, decimal amount, string currency, CancellationToken cancellationToken = default)
    {
        var id = ExpenseId.Create(expenseId);
        var expense = await _repository.GetByIdAsync(id, cancellationToken);
        if (expense?.GroupId is null) return null; // unknown or personal expense — nothing to allocate

        var money = Money.Create(amount, currency);
        var uid = UserId.Create(userId);

        // Upsert by (expense, user): the event is authoritative and may be redelivered, so this is
        // idempotent on amount. The userId came from a household-authorized event — no caller override.
        Share result;
        var existing = await _splitRepository.GetByExpenseAndUserAsync(id, uid, cancellationToken);

        // Enforce the Σ shares ≤ expense total invariant. This arrives via an authoritative,
        // redeliverable household event, so a violation must NOT throw (that would dead-letter and
        // redeliver forever) — log and skip instead.
        var others = await _splitRepository.SumForExpenseAsync(id, existing?.Id, cancellationToken);
        if (!ShareMath.Fits(others, money.Amount, expense.Amount.Amount))
        {
            _logger.LogWarning(
                "Skipping GroupShareAssigned for expense {ExpenseId} user {UserId}: amount {Amount} would push shares past the expense total {Total}.",
                expenseId, userId, money.Amount, expense.Amount.Amount);
            return null;
        }

        if (existing is not null)
        {
            existing.Update(expense, money);
            await _splitRepository.UpdateAsync(existing, cancellationToken);
            result = existing;
        }
        else
        {
            result = Share.Create(expense, uid, money);
            await _splitRepository.AddAsync(result, cancellationToken);
        }
        expense.RecordShareChange();
        await _repository.UpdateAsync(expense, cancellationToken);
        await _splitRepository.CommitAsync(cancellationToken);
        // The committed event drives the share journalLine (Dr Member / Cr Expense).
        return ExpenseMapper.ToShareResponse(result);
    }

    public async Task<SettlementOutcome?> MarkPaidAsync(MarkExpensePaidCommand request, CancellationToken cancellationToken = default)
    {
        var expense = await _repository.GetByIdAsync(ExpenseId.Create(request.ExpenseId), cancellationToken)
            ?? throw new InvalidOperationException("Expense not found.");

        var occurrenceDate = DateTime.SpecifyKind(request.OccurrenceDate.Date, DateTimeKind.Utc);

        if (expense.GroupId.HasValue)
        {
            // The share settles into whichever account funded the bill: PayerMember pays the
            // payer back (Dr Member:payer / Cr Member:debtor), GroupCash pays into the pot
            // (Dr Cash / Cr Member:debtor). The committed fact drives the journalLine.
            var userId = UserId.Create(request.CallerUserId);
            var split = await _splitRepository.GetByExpenseAndUserAsync(expense.Id, userId, cancellationToken)
                ?? throw new InvalidOperationException("No share found for this user on this expense.");

            // Idempotent on the ledger: if already settled for this occurrence, no-op.
            if (await _ledgerQuery.IsShareSettledAsync(split.Id.Value, occurrenceDate, cancellationToken))
                return null;

            // The fact names the payer as nominal payee; the funding account is resolved
            // downstream from the expense's FundingSource.
            var nominalTo = UserId.Create(expense.PayerUserId ?? expense.EnteredBy.Value);
            var valueDate = DateTime.UtcNow.Date;
            split.Settle(expense, nominalTo, occurrenceDate, valueDate);
            await _splitRepository.CommitAsync(cancellationToken);

            return new SettlementOutcome(
                expense.GroupId.Value.Value, expense.Id.Value, split.Id.Value,
                userId.Value, nominalTo.Value, split.Amount.Amount, split.Amount.Currency,
                occurrenceDate, valueDate,
                LedgerSources.Settlement(request.ExpenseId, occurrenceDate, request.CallerUserId),
                expense.FundingSource);
        }

        // Personal expense: record direct payment (no group ledger involved).
        if (!expense.IsOwnedBy(request.CallerUserId))
            throw new InvalidOperationException("Access denied.");

        // Raises the fact; the journalLine consumer settles the payable from whatever funded it.
        expense.RecordPersonalPayment(request.FundingAccountId, DateTime.UtcNow);
        await _repository.UpdateAsync(expense, cancellationToken);
        await _repository.CommitAsync(cancellationToken);
        return null;
    }

    public async Task<SettlementOutcome?> MarkUnpaidAsync(MarkExpenseUnpaidCommand request, CancellationToken cancellationToken = default)
    {
        var expense = await _repository.GetByIdAsync(ExpenseId.Create(request.ExpenseId), cancellationToken);
        if (expense is null) return null;

        var occurrenceDate = DateTime.SpecifyKind(request.OccurrenceDate.Date, DateTimeKind.Utc);

        if (expense.GroupId.HasValue)
        {
            // The reversal fact drives the contra entry, keyed by the same source.
            var userId = UserId.Create(request.CallerUserId);
            var split = await _splitRepository.GetByExpenseAndUserAsync(expense.Id, userId, cancellationToken);
            if (split is null) return null;

            // Idempotent: nothing to reverse if the ledger shows no active settlement.
            if (!await _ledgerQuery.IsShareSettledAsync(split.Id.Value, occurrenceDate, cancellationToken))
                return null;

            split.ReverseSettlement(expense, occurrenceDate);
            await _splitRepository.CommitAsync(cancellationToken);

            var nominalTo = UserId.Create(expense.PayerUserId ?? expense.EnteredBy.Value);
            return new SettlementOutcome(
                expense.GroupId.Value.Value, expense.Id.Value, split.Id.Value,
                userId.Value, nominalTo.Value, split.Amount.Amount, split.Amount.Currency,
                occurrenceDate, DateTime.UtcNow.Date,
                LedgerSources.Settlement(request.ExpenseId, occurrenceDate, request.CallerUserId),
                expense.FundingSource);
        }

        expense.ReversePersonalPayment();
        await _repository.UpdateAsync(expense, cancellationToken);
        await _repository.CommitAsync(cancellationToken);
        return null;
    }

    public async Task<VendorPaymentOutcome?> MarkVendorPaidAsync(MarkVendorPaidCommand request, CancellationToken cancellationToken = default)
    {
        var expense = await _repository.GetByIdAsync(ExpenseId.Create(request.ExpenseId), cancellationToken);
        if (expense is null || expense.GroupId is null) return null;

        if (!expense.IsManagedBy(request.CallerUserId))
            throw new InvalidOperationException("Only the bill's owner can mark it paid to the vendor.");

        var occurrenceDate = DateTime.SpecifyKind(request.OccurrenceDate.Date, DateTimeKind.Utc);

        // Idempotent: if the bill owes the vendor nothing it's already paid (or legacy cash-basis).
        if (await _ledgerQuery.IsVendorPaidAsync(request.ExpenseId, cancellationToken))
            return null;

        // Dr Vendor Payable / Cr funding — Cr Member:payer when a member fronted it,
        // Cr Cash from the pot. The committed fact carries the funding source.
        var paidByUserId = expense.FundingSource == FundingSource.PayerMember ? expense.PayerUserId : null;
        expense.RecordVendorPayment(occurrenceDate, expense.FundingSource, paidByUserId);
        await _repository.CommitAsync(cancellationToken);

        return new VendorPaymentOutcome(
            expense.GroupId.Value.Value, expense.Id.Value, expense.Amount.Amount, expense.Amount.Currency,
            expense.FundingSource, paidByUserId, occurrenceDate, DateTime.UtcNow.Date,
            LedgerSources.VendorPayment(request.ExpenseId, occurrenceDate));
    }

    public async Task<VendorPaymentOutcome?> MarkVendorUnpaidAsync(MarkVendorUnpaidCommand request, CancellationToken cancellationToken = default)
    {
        var expense = await _repository.GetByIdAsync(ExpenseId.Create(request.ExpenseId), cancellationToken);
        if (expense is null || expense.GroupId is null) return null;

        if (!expense.IsManagedBy(request.CallerUserId))
            throw new InvalidOperationException("Only the bill's owner can undo the vendor payment.");

        var occurrenceDate = DateTime.SpecifyKind(request.OccurrenceDate.Date, DateTimeKind.Utc);

        // Idempotent: if the bill still owes the vendor it was never marked paid — nothing to undo.
        if (!await _ledgerQuery.IsVendorPaidAsync(request.ExpenseId, cancellationToken))
            return null;

        expense.ReverseVendorPayment(occurrenceDate);
        await _repository.CommitAsync(cancellationToken);

        // VendorPaymentReversed drives the ledger contra entry (a no-op for legacy expenses
        // with no such entry).
        return new VendorPaymentOutcome(
            expense.GroupId.Value.Value, expense.Id.Value, expense.Amount.Amount, expense.Amount.Currency,
            expense.FundingSource, expense.PayerUserId, occurrenceDate, DateTime.UtcNow.Date,
            LedgerSources.VendorPayment(request.ExpenseId, occurrenceDate));
    }
}
