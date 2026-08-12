using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.Engines;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Managers;

/// <summary>Dr Expense / Cr Vendor Payable. Payer and funding source belong to the
/// vendor-payment and settlement commands, not the accrual.</summary>
/// <summary>
/// Opening a card or loan. `OpeningBalance` is what is owed TODAY — positive, because it is a
/// debt; it posts against opening-balance equity so the book balances from the first entry.
/// </summary>
public sealed record OpenDebtAccountCommand(
    Guid UserId,
    string Name,
    string Currency,
    decimal AnnualPercentageRate,
    decimal OpeningBalance,
    DateTime AsOf,
    decimal? CreditLimit = null,
    int? StatementDayOfMonth = null,
    int? PaymentDueDayOfMonth = null,
    decimal? MinimumPayment = null);

public sealed record PostChargeToLedgerCommand(
    Guid GroupId,
    Guid ChargeId,
    string Title,
    string Category,
    decimal Total,
    string Currency,
    DateTime Date,
    /// <summary>Whose action produced the entry. Null when nobody is behind it.</summary>
    Guid? PostedByUserId = null);

public sealed record RecordSettlementCommand(
    Guid GroupId,
    Guid ChargeId,
    Guid AllocationId,
    Guid FromUserId,
    Guid ToUserId,
    decimal Amount,
    string Currency,
    DateTime Occurrence,
    DateTime ValueDate,
    string Source,
    FundingSource FundingSource = FundingSource.PayerMember);

/// <summary>Informational only — the posting is driven by the emitted domain events,
/// not by this return value.</summary>
public sealed record SettlementOutcome(
    Guid GroupId,
    Guid ChargeId,
    Guid AllocationId,
    Guid FromUserId,
    Guid ToUserId,
    decimal Amount,
    string Currency,
    DateTime Occurrence,
    DateTime ValueDate,
    string Source,
    FundingSource FundingSource = FundingSource.PayerMember);

/// <summary>Clears Vendor Payable into the funding account — the payer's Member
/// account when a member fronted it, the shared Cash pool when pooled.</summary>
public sealed record RecordVendorPaymentCommand(
    Guid GroupId,
    Guid ChargeId,
    decimal Total,
    string Currency,
    FundingSource FundingSource,
    Guid? PaidByUserId,
    DateTime Occurrence,
    DateTime ValueDate,
    string Source);

/// <summary>Informational only — the posting is driven by the emitted domain events.</summary>
public sealed record VendorPaymentOutcome(
    Guid GroupId,
    Guid ChargeId,
    decimal Total,
    string Currency,
    FundingSource FundingSource,
    Guid? PaidByUserId,
    DateTime Occurrence,
    DateTime ValueDate,
    string Source);

/// <summary>Deterministic journal-entry source strings so a later reversal can find the entry.</summary>
public static class LedgerSources
{
    public static string Charge(Guid chargeId) => $"charge:{chargeId:N}";

    public static string Settlement(Guid chargeId, DateTime occurrence, Guid fromUserId)
        => $"settlement:{chargeId:N}:{occurrence:yyyyMMdd}:{fromUserId:N}";

    public static string VendorPayment(Guid chargeId, DateTime occurrence)
        => $"vendorpayment:{chargeId:N}:{occurrence:yyyyMMdd}";

    /// <summary>Per-allocation source so a member's share is journaled (and reversible) on its own,
    /// whether it was added at creation or later — Dr Member / Cr Expense under this key.</summary>
    public static string Allocation(Guid allocationId) => $"allocation:{allocationId:N}";
}

/// <summary>Ensures the ledger and accounts exist, journalizes and commits as ONE transaction.
/// Holds no debit/credit policy of its own.</summary>
public interface IBookkeepingManager
{
    /// <summary>Dr Expense / Cr Vendor Payable. Posts when missing, reverses and re-posts on a
    /// changed amount, category, title or date, no-ops when the books already match. Returns true
    /// when it re-journaled, which invalidates the allocation postings. Idempotent.</summary>
    Task<bool> SyncChargeAccrualAsync(PostChargeToLedgerCommand command, CancellationToken ct = default);

    /// <summary>Dr Member / Cr Expense, keyed per allocation so a share added after creation
    /// reverses independently. Posts when missing, re-posts on a changed amount or account,
    /// no-ops when it matches. Idempotent.</summary>
    Task SyncAllocationAsync(Guid groupId, Guid chargeId, string category, Guid userId, decimal amount, string currency, Guid allocationId, CancellationToken ct = default);

    /// <summary><c>Dr Vendor Payable / Cr Member:payer</c> when a member fronted it,
    /// <c>Dr Vendor Payable / Cr Cash</c> from the pot. Idempotent on the source.
    /// "Is it paid" is DERIVED from the Vendor Payable balance — no paid-flag is stored.</summary>
    Task RecordVendorPaymentAsync(RecordVendorPaymentCommand command, CancellationToken ct = default);

    /// <summary>Idempotent on the command's source.</summary>
    Task RecordSettlementAsync(RecordSettlementCommand command, CancellationToken ct = default);

    /// <summary>
    /// Posts a personal charge into the caller's OWN book: Dr Expense / Cr whatever paid for it.
    ///
    /// This never touches a group ledger — a cost only one person bore is not the household's —
    /// and it is the same convergent shape the group accrual uses, so a redelivery or a no-op edit
    /// falls out without writing anything.
    /// </summary>
    Task ConvergePersonalChargeAsync(Guid chargeId, CancellationToken ct = default);

    /// <summary>
    /// Opens a card or loan in the caller's own book, with its terms, and posts any balance
    /// already owed as <c>Dr Opening balance / Cr {debt}</c>.
    ///
    /// The balance is POSTED rather than stored, so from the first moment it is the ledger's
    /// answer and cannot drift from the entries beneath it. The user's ledger is opened here if
    /// they have never had one — the same lazy ensure the group side uses.
    /// </summary>
    Task<Guid> OpenDebtAccountAsync(OpenDebtAccountCommand command, CancellationToken ct = default);

    /// <summary>Reverses with mirror entries rather than deleting. The only place a
    /// settlement is undone.</summary>
    Task ReverseBySourceAsync(Guid groupId, string source, CancellationToken ct = default);

    /// <summary><c>Dr Member:to / Cr Member:from</c> — moves both toward zero, outside any
    /// single charge. Idempotent on <paramref name="source"/>.</summary>
    Task RecordMemberTransferAsync(Guid groupId, Guid fromUserId, Guid toUserId, decimal amount, string currency, string source, CancellationToken ct = default);

    /// <summary>Unwinds a charge from the books — reverses every active journal entry tagged with it
    /// (accrual, vendor payment, settlements) so a deactivated/deleted bill leaves no orphan Vendor
    /// Payable or member balances. Idempotent: nothing to do if already unwound.</summary>
    Task ReverseChargeAsync(Guid groupId, Guid chargeId, CancellationToken ct = default);

    // These CONVERGE the books from current DB state for a charge/allocation/settlement/vendor event:
    // they re-read the aggregate (the manager owns this orchestration, not the message consumer) and
    // sync the books to it, so the consumer stays a thin dedup-and-dispatch adapter with no domain I/O.

    /// <summary>Sync a group charge's accrual entry to its current state, then re-sync every share.
    /// No-ops for a personal, deleted or deactivated charge.</summary>
    Task ConvergeChargeAsync(Guid chargeId, CancellationToken ct = default);

    /// <summary>Sync one share posting to the allocation's current state, reversing instead if the
    /// allocation has vanished, which is what makes it order-insensitive.</summary>
    Task ConvergeAllocationAsync(Guid groupId, Guid allocationId, CancellationToken ct = default);

    /// <summary>The funding side mirrors the charge's <see cref="FundingSource"/>: PayerMember → the
    /// payer, GroupCash → the pot.</summary>
    Task RecordSettlementFromEventAsync(
        Guid groupId, Guid chargeId, Guid allocationId, Guid fromUserId, Guid toUserId,
        decimal amount, string currency, DateTime occurrence, DateTime valueDate, CancellationToken ct = default);

    /// <summary>No-ops for a personal charge.</summary>
    Task RecordVendorPaymentFromEventAsync(
        Guid chargeId, FundingSource fundingSource, Guid? paidByUserId,
        DateTime occurrence, DateTime paidAt, CancellationToken ct = default);

    /// <summary>No-ops for a personal charge.</summary>
    Task ReverseVendorPaymentFromEventAsync(Guid chargeId, DateTime occurrence, CancellationToken ct = default);
}

internal sealed class BookkeepingManager : IBookkeepingManager
{
    private readonly ILedgerRepository _ledgers;
    private readonly IJournalizingEngine _journalizing;
    private readonly IChargeRepository _charges;
    private readonly IAllocationRepository _allocations;

    public BookkeepingManager(
        ILedgerRepository ledgers,
        IJournalizingEngine journalizing,
        IChargeRepository charges,
        IAllocationRepository allocations)
    {
        _ledgers = ledgers;
        _journalizing = journalizing;
        _charges = charges;
        _allocations = allocations;
    }

    public async Task<bool> SyncChargeAccrualAsync(PostChargeToLedgerCommand cmd, CancellationToken ct = default)
    {
        var ledger = await _ledgers.GetOrOpenLedgerAsync(LedgerOwnerType.Group, cmd.GroupId, cmd.Currency, GroupChart.StandardAccounts, ct);

        var expenseAccount = await _ledgers.GetOrOpenAccountAsync(ledger.Id, GroupChart.Expense(ledger.Id, cmd.Category), ct);

        var source = LedgerSources.Charge(cmd.ChargeId);
        var date = DateTime.SpecifyKind(cmd.Date.Date, DateTimeKind.Utc);
        var description = $"{cmd.Title} — incurred";

        // Already in sync? One active accrual entry debiting the right expense account for the
        // right amount with the same description and value date — nothing to do. This is what
        // makes the event-driven posting convergent: redeliveries and no-op edits fall out here.
        var active = (await _ledgers.GetEntriesBySourceAsync(ledger.Id, source, ct)).InEffect();
        if (active.Count == 1 && active[0].SaysAccrual(expenseAccount.Id, cmd.Total, cmd.Currency, description, date))
            return false;

        // Stale (amount/category/title/date changed) — reverse what's on the books, then re-post.
        //
        // Reversed at the ORIGINAL entry's date, not today's. A correction and its re-post have to
        // land in the same period or that period is misstated: reversing a July accrual in August
        // and re-posting it to July leaves July carrying both the old amount and the new one, and a
        // balance sheet drawn at 31 July reports the sum of them. The cumulative position is right
        // either way, which is what makes the split version so quiet.
        foreach (var stale in active)
            await _ledgers.AddJournalEntryAsync(stale.Reverse(stale.Date), ct);

        // Accrual basis: the charge is incurred and OWED to the vendor — Dr Expense / Cr Vendor
        // Payable. Member shares are journaled per-allocation (SyncAllocationAsync) so a split added
        // after creation is tracked exactly like a create-time one. Who pays the vendor (the pot) is
        // recorded later. "Is it paid" is derived from the Vendor Payable balance — the ledger is the
        // single source of truth.
        var vendorPayable = await _ledgers.GetOrOpenAccountAsync(ledger.Id, GroupChart.VendorPayable(ledger.Id), ct);

        var draft = _journalizing.JournalizeTransfer(new TransferContext(
            DebitAccount: expenseAccount.Id, CreditAccount: vendorPayable.Id,
            Money.Create(cmd.Total, cmd.Currency), date, description, source));
        var entry = JournalEntry.Post(
            ledger.Id, draft.Date, draft.Description, draft.Lines, draft.Source,
            sourceChargeId: cmd.ChargeId, postedByUserId: cmd.PostedByUserId);
        await _ledgers.AddJournalEntryAsync(entry, ct);
        await _ledgers.CommitAsync(ct);
        return true;
    }

    public async Task SyncAllocationAsync(
        Guid groupId, Guid chargeId, string category, Guid userId, decimal amount, string currency,
        Guid allocationId, CancellationToken ct = default)
    {
        var ledger = await _ledgers.GetOrOpenLedgerAsync(LedgerOwnerType.Group, groupId, currency, GroupChart.StandardAccounts, ct);
        var source = LedgerSources.Allocation(allocationId);

        var expenseAccount = await _ledgers.GetOrOpenAccountAsync(ledger.Id, GroupChart.Expense(ledger.Id, category), ct);
        var member = await _ledgers.GetOrOpenAccountAsync(ledger.Id, GroupChart.Member(ledger.Id, userId), ct);

        // Already in sync? One active entry moving the right amount from the right member onto the
        // right expense account — nothing to do (redeliveries and unchanged upserts land here).
        var active = (await _ledgers.GetEntriesBySourceAsync(ledger.Id, source, ct)).InEffect();
        if (active.Count == 1 && active[0].SaysTransfer(member.Id, expenseAccount.Id, amount, currency))
            return;

        foreach (var stale in active)
            // Same rule as the accrual: a correction is reversed in the period it corrects.
            await _ledgers.AddJournalEntryAsync(stale.Reverse(stale.Date), ct);

        // The member bears their share — Dr Member / Cr Expense (moves the cost off the nominal
        // expense onto the member's stake).
        var draft = _journalizing.JournalizeTransfer(new TransferContext(
            DebitAccount: member.Id, CreditAccount: expenseAccount.Id,
            Money.Create(amount, currency), DateTime.UtcNow.Date, "Allocation", source));
        var entry = JournalEntry.Post(
            ledger.Id, draft.Date, draft.Description, draft.Lines, draft.Source,
            sourceChargeId: chargeId, sourceMemberId: userId, postedByUserId: userId);
        await _ledgers.AddJournalEntryAsync(entry, ct);
        await _ledgers.CommitAsync(ct);
    }

    public async Task RecordVendorPaymentAsync(RecordVendorPaymentCommand cmd, CancellationToken ct = default)
    {
        var ledger = await _ledgers.GetOrOpenLedgerAsync(LedgerOwnerType.Group, cmd.GroupId, cmd.Currency, GroupChart.StandardAccounts, ct);

        // Idempotent: if this occurrence's vendor payment is already on the books, do nothing.
        var existing = (await _ledgers.GetEntriesBySourceAsync(ledger.Id, cmd.Source, ct)).InEffect();
        if (existing.Count > 0) return;

        var vendorPayable = await _ledgers.GetOrOpenAccountAsync(ledger.Id, GroupChart.VendorPayable(ledger.Id), ct);
        // The funding account is whoever actually paid the vendor — the payer's own Member account
        // (front-and-reimburse) or the shared Cash pool (pooled). One resolver, one volatility axis.
        var funding = await _ledgers.GetOrOpenAccountAsync(
            ledger.Id, GroupChart.Funding(ledger.Id, cmd.FundingSource, cmd.PaidByUserId ?? Guid.Empty), ct);

        // Clear the liability against the funding account: Dr Vendor Payable / Cr funding.
        var draft = _journalizing.JournalizeTransfer(new TransferContext(
            DebitAccount: vendorPayable.Id, CreditAccount: funding.Id,
            Money.Create(cmd.Total, cmd.Currency), cmd.ValueDate, "Vendor payment", cmd.Source));

        var entry = JournalEntry.Post(
            ledger.Id, draft.Date, draft.Description, draft.Lines, draft.Source,
            sourceChargeId: cmd.ChargeId, sourceOccurrence: cmd.Occurrence,
            postedByUserId: cmd.PaidByUserId);
        await _ledgers.AddJournalEntryAsync(entry, ct);
        await _ledgers.CommitAsync(ct);
    }

    public async Task RecordSettlementAsync(RecordSettlementCommand cmd, CancellationToken ct = default)
    {
        var ledger = await _ledgers.GetOrOpenLedgerAsync(LedgerOwnerType.Group, cmd.GroupId, cmd.Currency, GroupChart.StandardAccounts, ct);

        // Idempotent: if this settlement is already on the books (active entry under its
        // source), do nothing. The ledger is the single source of truth — no dual write.
        var existing = (await _ledgers.GetEntriesBySourceAsync(ledger.Id, cmd.Source, ct)).InEffect();
        if (existing.Count > 0) return;

        var from = await _ledgers.GetOrOpenAccountAsync(ledger.Id, GroupChart.Member(ledger.Id, cmd.FromUserId), ct);
        // The debtor settles INTO the funding account that paid the vendor — the payer's Member
        // account (front-and-reimburse) or the shared Cash pool (pooled). Resolved the same way
        // the charge was posted, so a settlement always mirrors its charge's funding.
        var funding = await _ledgers.GetOrOpenAccountAsync(ledger.Id, GroupChart.Funding(ledger.Id, cmd.FundingSource, cmd.ToUserId), ct);

        // A settlement debits the funding account and credits the debtor — that direction is the
        // workflow's call; the engine just balances it.
        var draft = _journalizing.JournalizeTransfer(new TransferContext(
            DebitAccount: funding.Id, CreditAccount: from.Id,
            Money.Create(cmd.Amount, cmd.Currency), cmd.ValueDate, "Settlement", cmd.Source));

        // The ledger records only the accounting (with opaque source-document provenance for the
        // read side). The SettlementRecorded fact is raised by the Allocation aggregate in the
        // charge context — the ledger must not know about charge-domain events.
        var entry = JournalEntry.Post(
            ledger.Id, draft.Date, draft.Description, draft.Lines, draft.Source,
            sourceChargeId: cmd.ChargeId, sourceAllocationId: cmd.AllocationId,
            sourceOccurrence: cmd.Occurrence, sourceMemberId: cmd.FromUserId,
            postedByUserId: cmd.FromUserId);
        await _ledgers.AddJournalEntryAsync(entry, ct);

        await _ledgers.CommitAsync(ct);
    }

    public async Task ReverseBySourceAsync(Guid groupId, string source, CancellationToken ct = default)
    {
        var ledger = await _ledgers.GetLedgerByOwnerAsync(LedgerOwnerType.Group, groupId, ct);
        if (ledger is null) return;

        var active = (await _ledgers.GetEntriesBySourceAsync(ledger.Id, source, ct)).InEffect();
        if (active.Count == 0) return;

        // The ledger only mirrors the reversing entries; the SettlementReversed fact is raised by
        // the Allocation aggregate in the charge context.
        foreach (var entry in active)
            await _ledgers.AddJournalEntryAsync(entry.Reverse(DateTime.UtcNow.Date), ct);
        await _ledgers.CommitAsync(ct);
    }

    public async Task RecordMemberTransferAsync(Guid groupId, Guid fromUserId, Guid toUserId, decimal amount, string currency, string source, CancellationToken ct = default)
    {
        var ledger = await _ledgers.GetOrOpenLedgerAsync(LedgerOwnerType.Group, groupId, currency, GroupChart.StandardAccounts, ct);

        // Idempotent on source — re-posting the same settle-up payment is a no-op.
        var existing = (await _ledgers.GetEntriesBySourceAsync(ledger.Id, source, ct)).InEffect();
        if (existing.Count > 0) return;

        var from = await _ledgers.GetOrOpenAccountAsync(ledger.Id, GroupChart.Member(ledger.Id, fromUserId), ct);
        var to = await _ledgers.GetOrOpenAccountAsync(ledger.Id, GroupChart.Member(ledger.Id, toUserId), ct);

        // The payer (from, a debtor) squares up with a creditor (to): Dr Member:to / Cr Member:from,
        // moving both toward zero. The cash changed hands directly between the two — no pot involved.
        var draft = _journalizing.JournalizeTransfer(new TransferContext(
            DebitAccount: to.Id, CreditAccount: from.Id,
            Money.Create(amount, currency), DateTime.UtcNow.Date, "Settle-up", source));

        var entry = JournalEntry.Post(
            ledger.Id, draft.Date, draft.Description, draft.Lines, draft.Source,
            sourceMemberId: fromUserId, postedByUserId: fromUserId);
        await _ledgers.AddJournalEntryAsync(entry, ct);
        await _ledgers.CommitAsync(ct);
    }

    public async Task ReverseChargeAsync(Guid groupId, Guid chargeId, CancellationToken ct = default)
    {
        var ledger = await _ledgers.GetLedgerByOwnerAsync(LedgerOwnerType.Group, groupId, ct);
        if (ledger is null) return;

        // Every in-effect entry for the charge — accrual, vendor payment, and settlements all carry
        // its SourceChargeId. Reversing them returns every touched account to where it was.
        var active = (await _ledgers.GetEntriesByChargeAsync(ledger.Id, chargeId, ct)).InEffect();
        if (active.Count == 0) return;

        foreach (var entry in active)
            await _ledgers.AddJournalEntryAsync(entry.Reverse(DateTime.UtcNow.Date), ct);
        await _ledgers.CommitAsync(ct);
    }

    public async Task ConvergeChargeAsync(Guid chargeId, CancellationToken ct = default)
    {
        var charge = await _charges.GetByIdAsync(ChargeId.Create(chargeId), ct);
        if (charge is null) return;

        // A personal cost belongs in the person's own book, never the household's. Routed here
        // rather than in the consumer so there is one answer to "where does this post".
        if (charge.GroupId is null)
        {
            await ConvergePersonalChargeAsync(chargeId, ct);
            return;
        }

        if (!charge.IsActive) return;

        var groupId = charge.GroupId.Value.Value;
        var date = charge.RecurrenceSchedule?.CurrentOccurrence(charge.DueDate) ?? charge.DueDate;
        await SyncChargeAccrualAsync(new PostChargeToLedgerCommand(
            groupId, chargeId, charge.Title, charge.Category.ToString(),
            charge.Amount.Amount, charge.Amount.Currency, date,
            // Whoever entered the bill. Not stored on the event, so it is read from the charge
            // the converge already loaded — no wire contract has to change to get an audit trail.
            charge.CreatedBy?.Value), ct);

        // Re-sync every share — allocations credit the category's expense account, so a category
        // change moves them too. Each sync is a cheap no-op when the books already match.
        var allocations = await _allocations.ListByChargeAsync(charge.Id, ct);
        foreach (var a in allocations)
            await SyncAllocationAsync(
                groupId, chargeId, charge.Category.ToString(),
                a.UserId.Value, a.Amount.Amount, a.Amount.Currency, a.Id.Value, ct);
    }

    public async Task ConvergeAllocationAsync(Guid groupId, Guid allocationId, CancellationToken ct = default)
    {
        var allocation = await _allocations.GetByIdAsync(AllocationId.Create(allocationId), ct);
        if (allocation is null)
        {
            // Removed before this message was processed — reverse instead (order-insensitive).
            await ReverseBySourceAsync(groupId, LedgerSources.Allocation(allocationId), ct);
            return;
        }

        var charge = await _charges.GetByIdAsync(allocation.ChargeId, ct);
        if (charge?.GroupId is null || !charge.IsActive) return;

        await SyncAllocationAsync(
            groupId, charge.Id.Value, charge.Category.ToString(),
            allocation.UserId.Value, allocation.Amount.Amount, allocation.Amount.Currency, allocationId, ct);
    }

    public async Task RecordSettlementFromEventAsync(
        Guid groupId, Guid chargeId, Guid allocationId, Guid fromUserId, Guid toUserId,
        decimal amount, string currency, DateTime occurrence, DateTime valueDate, CancellationToken ct = default)
    {
        // The settlement mirrors the charge's funding model: PayerMember settles to the payer
        // (ToUserId), GroupCash into the pot. Read the charge for the authoritative funding source.
        var charge = await _charges.GetByIdAsync(ChargeId.Create(chargeId), ct);
        var fundingSource = charge?.FundingSource ?? FundingSource.GroupCash;
        await RecordSettlementAsync(new RecordSettlementCommand(
            groupId, chargeId, allocationId, fromUserId, toUserId, amount, currency,
            occurrence, valueDate,
            LedgerSources.Settlement(chargeId, occurrence, fromUserId),
            fundingSource), ct);
    }

    public async Task RecordVendorPaymentFromEventAsync(
        Guid chargeId, FundingSource fundingSource, Guid? paidByUserId,
        DateTime occurrence, DateTime paidAt, CancellationToken ct = default)
    {
        // The event names only the occurrence; the amount and group come off the charge.
        var charge = await _charges.GetByIdAsync(ChargeId.Create(chargeId), ct);
        if (charge?.GroupId is null) return;
        await RecordVendorPaymentAsync(new RecordVendorPaymentCommand(
            charge.GroupId.Value.Value, chargeId, charge.Amount.Amount, charge.Amount.Currency,
            fundingSource, paidByUserId, occurrence, paidAt,
            LedgerSources.VendorPayment(chargeId, occurrence)), ct);
    }

    public async Task ReverseVendorPaymentFromEventAsync(Guid chargeId, DateTime occurrence, CancellationToken ct = default)
    {
        var charge = await _charges.GetByIdAsync(ChargeId.Create(chargeId), ct);
        if (charge?.GroupId is null) return;
        await ReverseBySourceAsync(
            charge.GroupId.Value.Value, LedgerSources.VendorPayment(chargeId, occurrence), ct);
    }

    /// <summary>True when the active accrual entry already reflects the charge — same expense
    /// account, amount, description (which carries the title) and value date.</summary>



    public async Task ConvergePersonalChargeAsync(Guid chargeId, CancellationToken ct = default)
    {
        var charge = await _charges.GetByIdAsync(ChargeId.Create(chargeId), ct);
        if (charge is null || charge.GroupId is not null) return;

        var ledger = await _ledgers.GetOrOpenLedgerAsync(LedgerOwnerType.User, charge.UserId.Value, charge.Amount.Currency, PersonalChart.StandardAccounts, ct);
        var source = LedgerSources.Charge(chargeId);
        var date = DateTime.SpecifyKind(charge.OccurrenceDate.Date, DateTimeKind.Utc);

        var active = (await _ledgers.GetEntriesBySourceAsync(ledger.Id, source, ct)).InEffect();

        // Deactivated or deleted: take it off the books and stop.
        if (!charge.IsActive)
        {
            foreach (var stale in active)
                await _ledgers.AddJournalEntryAsync(stale.Reverse(stale.Date), ct);
            if (active.Count > 0) await _ledgers.CommitAsync(ct);
            return;
        }

        var expenseAccount = await _ledgers.GetOrOpenAccountAsync(
            ledger.Id, PersonalChart.Expense(ledger.Id, charge.Category.ToString()), ct);

        // Whatever paid for it: a card they told us about, else their cash account.
        var fundingAccount = charge.FundingAccountId is { } declared
            ? await _ledgers.GetAccountAsync(AccountId.Create(declared), ct)
              ?? throw new InvalidOperationException("That funding account is not in this book.")
            : await _ledgers.GetOrOpenAccountAsync(ledger.Id, PersonalChart.Cash(ledger.Id), ct);

        var description = $"{charge.Title} — paid";
        if (active.Count == 1 && active[0].SaysAccrual(expenseAccount.Id, charge.Amount.Amount, charge.Amount.Currency, description, date))
            return;

        foreach (var stale in active)
            await _ledgers.AddJournalEntryAsync(stale.Reverse(stale.Date), ct);

        // Cr the card and the debt grows; Cr cash and it shrinks. One posting, both meanings,
        // because the account type already carries the difference.
        var draft = _journalizing.JournalizeTransfer(new TransferContext(
            DebitAccount: expenseAccount.Id, CreditAccount: fundingAccount.Id,
            charge.Amount, date, description, source));

        await _ledgers.AddJournalEntryAsync(
            JournalEntry.Post(ledger.Id, draft.Date, draft.Description, draft.Lines, draft.Source,
                sourceChargeId: chargeId, postedByUserId: charge.UserId.Value),
            ct);
        await _ledgers.CommitAsync(ct);
    }

    public async Task<Guid> OpenDebtAccountAsync(OpenDebtAccountCommand cmd, CancellationToken ct = default)
    {
        var ledger = await _ledgers.GetOrOpenLedgerAsync(LedgerOwnerType.User, cmd.UserId, cmd.Currency, PersonalChart.StandardAccounts, ct);

        var accountKey = Guid.NewGuid();
        var account = PersonalChart.OpenDebtAccount(ledger.Id, accountKey, cmd.Name);
        await _ledgers.AddAccountAsync(account, ct);

        var terms = DebtTerms.For(
            account.Id,
            UserId.Create(cmd.UserId),
            cmd.AnnualPercentageRate,
            cmd.CreditLimit,
            cmd.StatementDayOfMonth,
            cmd.PaymentDueDayOfMonth,
            cmd.MinimumPayment);
        await _ledgers.AddDebtTermsAsync(terms, ct);

        // Nothing owed yet needs no entry — a zero posting would not validate, and an account
        // with no postings already reads as a zero balance.
        if (cmd.OpeningBalance > 0m)
        {
            var opening = await _ledgers.GetOrOpenAccountAsync(ledger.Id, PersonalChart.OpeningBalance(ledger.Id), ct);

            var draft = _journalizing.JournalizeTransfer(new TransferContext(
                DebitAccount: opening.Id,
                CreditAccount: account.Id,
                Amount: Money.Create(cmd.OpeningBalance, cmd.Currency),
                ValueDate: cmd.AsOf,
                Description: $"{cmd.Name} — balance carried in",
                Source: $"debt-opening:{account.Id.Value:N}"));

            await _ledgers.AddJournalEntryAsync(
                JournalEntry.Post(ledger.Id, draft.Date, draft.Description, draft.Lines, draft.Source,
                    postedByUserId: cmd.UserId),
                ct);
        }

        await _ledgers.CommitAsync(ct);
        return account.Id.Value;
    }


}
