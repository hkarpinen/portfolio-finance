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
    Guid CallerUserId,
    string Name,
    string Currency,
    decimal AnnualPercentageRate,
    decimal OpeningBalance,
    DateTime AsOf,
    decimal? CreditLimit = null,
    int? StatementDayOfMonth = null,
    int? PaymentDueDayOfMonth = null,
    decimal? MinimumPayment = null);

/// <summary>
/// A bank account somebody connected, given a place in their own books.
///
/// <paramref name="IsCredit"/> decides which side it sits on: money you hold is an asset, money you
/// owe on a card or loan is a liability. Nothing else about the posting changes — a purchase
/// credits either one.
///
/// <paramref name="Balance"/> is what the provider says is there today, carried in against opening
/// equity so the book balances from its first entry. Positive means "you hold this" for a cash
/// account and "you owe this" for a card, which is how a provider reports both.
/// </summary>
public sealed record OpenBankAccountCommand(
    Guid CallerUserId,
    string Name,
    string Currency,
    bool IsCredit,
    decimal Balance,
    DateTime AsOf);

public sealed record PostExpenseToLedgerCommand(
    Guid GroupId,
    Guid ExpenseId,
    string Title,
    string Category,
    decimal Total,
    string Currency,
    DateTime Date,
    /// <summary>Whose action produced the entry. Null when nobody is behind it.</summary>
    Guid? PostedByUserId = null);

public sealed record RecordSettlementCommand(
    Guid GroupId,
    Guid ExpenseId,
    Guid ShareId,
    Guid FromUserId,
    Guid ToUserId,
    decimal Amount,
    string Currency,
    DateTime Occurrence,
    DateTime ValueDate,
    string Source,
    FundingSource FundingSource = FundingSource.PayerMember);

/// <summary>Informational only — the journalLine is driven by the emitted domain events,
/// not by this return value.</summary>
public sealed record SettlementOutcome(
    Guid GroupId,
    Guid ExpenseId,
    Guid ShareId,
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
    Guid ExpenseId,
    decimal Total,
    string Currency,
    FundingSource FundingSource,
    Guid? PaidByUserId,
    DateTime Occurrence,
    DateTime ValueDate,
    string Source);

/// <summary>Informational only — the journalLine is driven by the emitted domain events.</summary>
public sealed record VendorPaymentOutcome(
    Guid GroupId,
    Guid ExpenseId,
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
    public static string Expense(Guid expenseId) => $"expense:{expenseId:N}";

    public static string Settlement(Guid expenseId, DateTime occurrence, Guid fromUserId)
        => $"settlement:{expenseId:N}:{occurrence:yyyyMMdd}:{fromUserId:N}";

    public static string VendorPayment(Guid expenseId, DateTime occurrence)
        => $"vendorpayment:{expenseId:N}:{occurrence:yyyyMMdd}";

    /// <summary>Per-share source so a member's share is journaled (and reversible) on its own,
    /// whether it was added at creation or later — Dr Member / Cr Expense under this key.</summary>
    public static string Share(Guid shareId) => $"share:{shareId:N}";
}

/// <summary>Ensures the ledger and accounts exist, journalizes and commits as ONE transaction.
/// Holds no debit/credit policy of its own.</summary>
public interface IBookkeepingManager
{
    /// <summary>Dr Expense / Cr Vendor Payable. Posts when missing, reverses and re-posts on a
    /// changed amount, category, title or date, no-ops when the books already match. Returns true
    /// when it re-journaled, which invalidates the share lines. Idempotent.</summary>
    Task<bool> SyncExpenseAccrualAsync(PostExpenseToLedgerCommand command, CancellationToken ct = default);

    /// <summary>Dr Member / Cr Expense, keyed per share so a share added after creation
    /// reverses independently. Posts when missing, re-posts on a changed amount or account,
    /// no-ops when it matches. Idempotent.</summary>
    Task SyncShareAsync(Guid groupId, Guid expenseId, string category, Guid userId, decimal amount, string currency, Guid shareId, CancellationToken ct = default);

    /// <summary><c>Dr Vendor Payable / Cr Member:payer</c> when a member fronted it,
    /// <c>Dr Vendor Payable / Cr Cash</c> from the pot. Idempotent on the source.
    /// "Is it paid" is DERIVED from the Vendor Payable balance — no paid-flag is stored.</summary>
    Task RecordVendorPaymentAsync(RecordVendorPaymentCommand command, CancellationToken ct = default);

    /// <summary>Idempotent on the command's source.</summary>
    Task RecordSettlementAsync(RecordSettlementCommand command, CancellationToken ct = default);

    /// <summary>
    /// Settles a personal expense: <c>Dr Payable / Cr</c> whichever account funded it.
    ///
    /// Cash, checking or a card — the company was paid either way, and the funding account only
    /// says where the money came from. Crediting a card moves the debt from the company to the
    /// card issuer, which is exactly what happened.
    /// </summary>
    Task RecordPersonalPaymentAsync(
        Guid expenseId, Guid? fundingAccountId, DateTime valueDate, CancellationToken ct = default);

    /// <summary>
    /// Unwinds a personal settlement with a mirror entry, leaving the payable owed again. The
    /// original stands: the money did move, and then it moved back.
    /// </summary>
    Task ReversePersonalPaymentAsync(Guid expenseId, CancellationToken ct = default);

    /// <summary>
    /// Posts every personal expense whose day has arrived and which is not on the books yet, and
    /// returns how many that was. The other half of "a passing period becomes an expense": a bill
    /// entered ahead of time waits here until the date it belongs to.
    /// </summary>
    Task<int> PostDuePersonalExpensesAsync(Guid userId, DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Posts a personal expense into the caller's OWN book: Dr Expense / Cr whatever paid for it.
    ///
    /// This never touches a group ledger — a cost only one person bore is not the group's —
    /// and it is the same convergent shape the group accrual uses, so a redelivery or a no-op edit
    /// falls out without writing anything.
    /// </summary>
    Task ConvergePersonalExpenseAsync(Guid expenseId, CancellationToken ct = default);

    /// <summary>
    /// Opens a card or loan in the caller's own book, with its terms, and posts any balance
    /// already owed as <c>Dr Opening balance / Cr {debt}</c>.
    ///
    /// The balance is POSTED rather than stored, so from the first moment it is the ledger's
    /// answer and cannot drift from the entries beneath it. The user's ledger is opened here if
    /// they have never had one — the same lazy ensure the group side uses.
    /// </summary>
    Task<Guid> OpenDebtAccountAsync(OpenDebtAccountCommand command, CancellationToken ct = default);

    /// <summary>
    /// Opens a connected bank account in the owner's ledger and carries in its balance. Returns the
    /// ledger account id, which is what a transaction from that account posts against.
    /// </summary>
    Task<Guid> OpenBankAccountAsync(OpenBankAccountCommand command, CancellationToken ct = default);

    /// <summary>Reverses with mirror entries rather than deleting. The only place a
    /// settlement is undone.</summary>
    Task ReverseBySourceAsync(Guid groupId, string source, CancellationToken ct = default);

    /// <summary><c>Dr Member:to / Cr Member:from</c> — moves both toward zero, outside any
    /// single expense. Idempotent on <paramref name="source"/>.</summary>
    Task RecordMemberTransferAsync(Guid groupId, Guid fromUserId, Guid toUserId, decimal amount, string currency, string source, CancellationToken ct = default);

    /// <summary>Unwinds an expense from the books — reverses every active journal entry tagged with it
    /// (accrual, vendor payment, settlements) so a deactivated/deleted bill leaves no orphan Vendor
    /// Payable or member balances. Idempotent: nothing to do if already unwound.</summary>
    Task ReverseExpenseAsync(Guid groupId, Guid expenseId, CancellationToken ct = default);

    // These CONVERGE the books from current DB state for an expense/share/settlement/vendor event:
    // they re-read the aggregate (the manager owns this orchestration, not the message consumer) and
    // sync the books to it, so the consumer stays a thin dedup-and-dispatch adapter with no domain I/O.

    /// <summary>Sync a group expense's accrual entry to its current state, then re-sync every share.
    /// No-ops for a personal, deleted or deactivated expense.</summary>
    Task ConvergeExpenseAsync(Guid expenseId, CancellationToken ct = default);

    /// <summary>Sync one share journalLine to the share's current state, reversing instead if the
    /// share has vanished, which is what makes it order-insensitive.</summary>
    Task ConvergeShareAsync(Guid groupId, Guid shareId, CancellationToken ct = default);

    /// <summary>The funding side mirrors the expense's <see cref="FundingSource"/>: PayerMember → the
    /// payer, GroupCash → the pot.</summary>
    Task RecordSettlementFromEventAsync(
        Guid groupId, Guid expenseId, Guid shareId, Guid fromUserId, Guid toUserId,
        decimal amount, string currency, DateTime occurrence, DateTime valueDate, CancellationToken ct = default);

    /// <summary>No-ops for a personal expense.</summary>
    Task RecordVendorPaymentFromEventAsync(
        Guid expenseId, FundingSource fundingSource, Guid? paidByUserId,
        DateTime occurrence, DateTime paidAt, CancellationToken ct = default);

    /// <summary>No-ops for a personal expense.</summary>
    Task ReverseVendorPaymentFromEventAsync(Guid expenseId, DateTime occurrence, CancellationToken ct = default);

    /// <summary>
    /// Money arriving: Dr the account it landed in, Cr where it came from. Converges, so a
    /// redelivery or a corrected amount both settle to one entry.
    /// </summary>
    Task ConvergeReceiptAsync(Guid receiptId, CancellationToken ct = default);

    /// <summary>Takes a receipt off the books — it did not arrive after all.</summary>
    Task ReverseReceiptAsync(Guid receiptId, CancellationToken ct = default);
}

internal sealed class BookkeepingManager : IBookkeepingManager
{
    private readonly ILedgerRepository _ledgers;
    private readonly IExpenseRepository _expenses;
    private readonly IShareRepository _shares;
    private readonly IReceiptRepository _receipts;

    public BookkeepingManager(
        ILedgerRepository ledgers,
        IExpenseRepository expenses,
        IShareRepository shares,
        IReceiptRepository receipts)
    {
        _ledgers = ledgers;
        _expenses = expenses;
        _shares = shares;
        _receipts = receipts;
    }

    public async Task<bool> SyncExpenseAccrualAsync(PostExpenseToLedgerCommand cmd, CancellationToken ct = default)
    {
        var ledger = await _ledgers.GetOrOpenLedgerAsync(AccountingEntity.Group(cmd.GroupId), cmd.Currency, ct);

        var expenseAccount = await _ledgers.GetOrOpenAccountAsync(ledger.Id, Chart.Expense(ledger.Id, cmd.Category), ct);

        var source = LedgerSources.Expense(cmd.ExpenseId);
        var vendorPayable = await _ledgers.GetOrOpenAccountAsync(ledger.Id, Chart.Payable(ledger.Id), ct);

        // Member shares are journaled per-share (SyncShareAsync) so a split added later is tracked
        // exactly like a create-time one, and who pays the vendor is recorded separately.
        return await _ledgers.ConvergeAsync(Journalize.ExpenseIncurred(
            ledger.Id, expenseAccount.Id, vendorPayable.Id,
            Money.Create(cmd.Total, cmd.Currency), cmd.Date, cmd.Title, source,
            cmd.ExpenseId, cmd.PostedByUserId), ct: ct);
    }

    public async Task SyncShareAsync(
        Guid groupId, Guid expenseId, string category, Guid userId, decimal amount, string currency,
        Guid shareId, CancellationToken ct = default)
    {
        var ledger = await _ledgers.GetOrOpenLedgerAsync(AccountingEntity.Group(groupId), currency, ct);
        var source = LedgerSources.Share(shareId);

        var expenseAccount = await _ledgers.GetOrOpenAccountAsync(ledger.Id, Chart.Expense(ledger.Id, category), ct);
        var member = await _ledgers.GetOrOpenAccountAsync(ledger.Id, Chart.Member(ledger.Id, userId), ct);

        await _ledgers.ConvergeAsync(Journalize.ShareBorne(
            ledger.Id, member.Id, expenseAccount.Id,
            Money.Create(amount, currency), DateTime.UtcNow.Date, source,
            expenseId, userId), ct: ct);
    }

    public async Task RecordVendorPaymentAsync(RecordVendorPaymentCommand cmd, CancellationToken ct = default)
    {
        var ledger = await _ledgers.GetOrOpenLedgerAsync(AccountingEntity.Group(cmd.GroupId), cmd.Currency, ct);

        var vendorPayable = await _ledgers.GetOrOpenAccountAsync(ledger.Id, Chart.Payable(ledger.Id), ct);
        // The funding account is whoever actually paid the vendor — the payer's own Member account
        // (front-and-reimburse) or the shared Cash pool (pooled). One resolver, one volatility axis.
        var funding = await _ledgers.GetOrOpenAccountAsync(
            ledger.Id, Chart.Funding(ledger.Id, cmd.FundingSource, cmd.PaidByUserId ?? Guid.Empty), ct);

        await _ledgers.ConvergeAsync(Journalize.VendorPaid(
            ledger.Id, vendorPayable.Id, funding.Id,
            Money.Create(cmd.Total, cmd.Currency), cmd.ValueDate, "Vendor payment", cmd.Source,
            cmd.ExpenseId, cmd.Occurrence, cmd.PaidByUserId), postOnce: true, ct: ct);
    }

    public async Task RecordSettlementAsync(RecordSettlementCommand cmd, CancellationToken ct = default)
    {
        var ledger = await _ledgers.GetOrOpenLedgerAsync(AccountingEntity.Group(cmd.GroupId), cmd.Currency, ct);

        var from = await _ledgers.GetOrOpenAccountAsync(ledger.Id, Chart.Member(ledger.Id, cmd.FromUserId), ct);
        // The debtor settles INTO the funding account that paid the vendor — the payer's Member
        // account (front-and-reimburse) or the shared Cash pool (pooled). Resolved the same way
        // the expense was posted, so a settlement always mirrors its expense's funding.
        var funding = await _ledgers.GetOrOpenAccountAsync(ledger.Id, Chart.Funding(ledger.Id, cmd.FundingSource, cmd.ToUserId), ct);

        // A settlement debits the funding account and credits the debtor — that direction is the
        // workflow's call; the engine just balances it.
        // The ledger records only the accounting (with opaque source-document provenance for the
        // read side). The SettlementRecorded fact is raised by the Share aggregate in the
        // expense context — the ledger must not know about expense-domain events.
        await _ledgers.ConvergeAsync(Journalize.Settlement(
            ledger.Id, funding.Id, from.Id,
            Money.Create(cmd.Amount, cmd.Currency), cmd.ValueDate, cmd.Source,
            cmd.ExpenseId, cmd.ShareId, cmd.Occurrence, cmd.FromUserId), postOnce: true, ct: ct);
    }

    public async Task ReverseBySourceAsync(Guid groupId, string source, CancellationToken ct = default)
    {
        var ledger = await _ledgers.GetLedgerByOwnerAsync(AccountingEntity.Group(groupId), ct);
        if (ledger is null) return;

        var active = (await _ledgers.GetEntriesBySourceAsync(ledger.Id, source, ct)).InEffect();
        if (active.Count == 0) return;

        // The ledger only mirrors the reversing entries; the SettlementReversed fact is raised by
        // the Share aggregate in the expense context.
        foreach (var entry in active)
            await _ledgers.AddJournalEntryAsync(entry.Reverse(DateTime.UtcNow.Date), ct);
        await _ledgers.CommitAsync(ct);
    }

    public async Task RecordMemberTransferAsync(Guid groupId, Guid fromUserId, Guid toUserId, decimal amount, string currency, string source, CancellationToken ct = default)
    {
        // Both rules were enforced only in the controller, so anything reaching this from a
        // consumer or a test could post a settle-up with itself — two legs on ONE member account,
        // which nets to nothing and passes every check the journal makes.
        if (fromUserId == toUserId)
            throw new InvalidOperationException("Nobody settles up with themselves.");
        if (amount <= 0m)
            throw new ArgumentException("A settle-up moves a positive amount.", nameof(amount));

        var ledger = await _ledgers.GetOrOpenLedgerAsync(AccountingEntity.Group(groupId), currency, ct);

        var from = await _ledgers.GetOrOpenAccountAsync(ledger.Id, Chart.Member(ledger.Id, fromUserId), ct);
        var to = await _ledgers.GetOrOpenAccountAsync(ledger.Id, Chart.Member(ledger.Id, toUserId), ct);

        // The payer (from, a debtor) squares up with a creditor (to): Dr Member:to / Cr Member:from,
        // moving both toward zero. The cash changed hands directly between the two — no pot involved.
        await _ledgers.ConvergeAsync(Journalize.SettleUp(
            ledger.Id, to.Id, from.Id,
            Money.Create(amount, currency), DateTime.UtcNow.Date, source, fromUserId), postOnce: true, ct: ct);
    }

    public async Task ReverseExpenseAsync(Guid groupId, Guid expenseId, CancellationToken ct = default)
    {
        var ledger = await _ledgers.GetLedgerByOwnerAsync(AccountingEntity.Group(groupId), ct);
        if (ledger is null) return;

        // Every in-effect entry for the expense — accrual, vendor payment, and settlements all carry
        // its SourceExpenseId. Reversing them returns every touched account to where it was.
        var active = (await _ledgers.GetEntriesByExpenseAsync(ledger.Id, expenseId, ct)).InEffect();
        if (active.Count == 0) return;

        foreach (var entry in active)
            await _ledgers.AddJournalEntryAsync(entry.Reverse(DateTime.UtcNow.Date), ct);
        await _ledgers.CommitAsync(ct);
    }

    public async Task ConvergeExpenseAsync(Guid expenseId, CancellationToken ct = default)
    {
        var expense = await _expenses.GetByIdAsync(ExpenseId.Create(expenseId), ct);
        if (expense is null) return;

        // A personal cost belongs in the person's own book, never the group's. Routed here
        // rather than in the consumer so there is one answer to "where does this post".
        if (expense.GroupId is null)
        {
            await ConvergePersonalExpenseAsync(expenseId, ct);
            return;
        }

        if (!expense.IsActive) return;

        var groupId = expense.GroupId.Value.Value;
        // The occurrence this expense IS, not whichever one the calendar has reached. Deriving it
        // from a recurrence made one entry's value date move every month, which is the drift the
        // schedule split exists to stop.
        var date = expense.OccurrenceDate;
        await SyncExpenseAccrualAsync(new PostExpenseToLedgerCommand(
            groupId, expenseId, expense.Title, expense.Category.ToString(),
            expense.Amount.Amount, expense.Amount.Currency, date,
            // Whoever entered the bill. Not stored on the event, so it is read from the expense
            // the converge already loaded — no wire contract has to change to get an audit trail.
            expense.EnteredBy.Value), ct);

        // Re-sync every share — shares credit the category's expense account, so a category
        // change moves them too. Each sync is a cheap no-op when the books already match.
        var shares = await _shares.ListByExpenseAsync(expense.Id, ct);
        foreach (var a in shares)
            await SyncShareAsync(
                groupId, expenseId, expense.Category.ToString(),
                a.UserId.Value, a.Amount.Amount, a.Amount.Currency, a.Id.Value, ct);
    }

    public async Task ConvergeShareAsync(Guid groupId, Guid shareId, CancellationToken ct = default)
    {
        var share = await _shares.GetByIdAsync(ShareId.Create(shareId), ct);
        if (share is null)
        {
            // Removed before this message was processed — reverse instead (order-insensitive).
            await ReverseBySourceAsync(groupId, LedgerSources.Share(shareId), ct);
            return;
        }

        var expense = await _expenses.GetByIdAsync(share.ExpenseId, ct);
        if (expense?.GroupId is null || !expense.IsActive) return;

        await SyncShareAsync(
            groupId, expense.Id.Value, expense.Category.ToString(),
            share.UserId.Value, share.Amount.Amount, share.Amount.Currency, shareId, ct);
    }

    public async Task RecordSettlementFromEventAsync(
        Guid groupId, Guid expenseId, Guid shareId, Guid fromUserId, Guid toUserId,
        decimal amount, string currency, DateTime occurrence, DateTime valueDate, CancellationToken ct = default)
    {
        // The settlement mirrors the expense's funding model: PayerMember settles to the payer
        // (ToUserId), GroupCash into the pot. Read the expense for the authoritative funding source.
        var expense = await _expenses.GetByIdAsync(ExpenseId.Create(expenseId), ct);
        var fundingSource = expense?.FundingSource ?? FundingSource.GroupCash;
        await RecordSettlementAsync(new RecordSettlementCommand(
            groupId, expenseId, shareId, fromUserId, toUserId, amount, currency,
            occurrence, valueDate,
            LedgerSources.Settlement(expenseId, occurrence, fromUserId),
            fundingSource), ct);
    }

    public async Task RecordVendorPaymentFromEventAsync(
        Guid expenseId, FundingSource fundingSource, Guid? paidByUserId,
        DateTime occurrence, DateTime paidAt, CancellationToken ct = default)
    {
        // The event names only the occurrence; the amount and group come off the expense.
        var expense = await _expenses.GetByIdAsync(ExpenseId.Create(expenseId), ct);
        if (expense?.GroupId is null) return;
        await RecordVendorPaymentAsync(new RecordVendorPaymentCommand(
            expense.GroupId.Value.Value, expenseId, expense.Amount.Amount, expense.Amount.Currency,
            fundingSource, paidByUserId, occurrence, paidAt,
            LedgerSources.VendorPayment(expenseId, occurrence)), ct);
    }

    public async Task ReverseVendorPaymentFromEventAsync(Guid expenseId, DateTime occurrence, CancellationToken ct = default)
    {
        var expense = await _expenses.GetByIdAsync(ExpenseId.Create(expenseId), ct);
        if (expense?.GroupId is null) return;
        await ReverseBySourceAsync(
            expense.GroupId.Value.Value, LedgerSources.VendorPayment(expenseId, occurrence), ct);
    }

    /// <summary>True when the active accrual entry already reflects the expense — same expense
    /// account, amount, description (which carries the title) and value date.</summary>



    public async Task ConvergePersonalExpenseAsync(Guid expenseId, CancellationToken ct = default)
    {
        var expense = await _expenses.GetByIdAsync(ExpenseId.Create(expenseId), ct);
        if (expense is null || expense.GroupId is not null) return;

        // Not yet. Somebody recording next month's expense has not spent the money, and journalLine it
        // now would book a cost that has not happened — the same rule that stops CatchUp writing
        // past today. PostDuePersonalExpensesAsync picks it up when the day comes.
        if (expense.IsActive && expense.OccurrenceDate.Date > DateTime.UtcNow.Date) return;

        var ledger = await _ledgers.GetOrOpenLedgerAsync(expense.Owner, expense.Amount.Currency, ct);
        var source = LedgerSources.Expense(expenseId);
        var date = DateTime.SpecifyKind(expense.OccurrenceDate.Date, DateTimeKind.Utc);

        // Deactivated or deleted: take it off the books and stop.
        if (!expense.IsActive)
        {
            var inEffect = (await _ledgers.GetEntriesBySourceAsync(ledger.Id, source, ct)).InEffect();
            foreach (var stale in ConvergencePlan.Remove(inEffect).Reverse)
                await _ledgers.AddJournalEntryAsync(stale.Reverse(stale.Date), ct);
            if (inEffect.Count > 0) await _ledgers.CommitAsync(ct);
            return;
        }

        var expenseAccount = await _ledgers.GetOrOpenAccountAsync(
            ledger.Id, Chart.Expense(ledger.Id, expense.Category.ToString()), ct);

        // What is owed until it is settled. Collapsing this into the funding account would post
        // the cost and its payment as one movement, and "has this been paid" would have nothing
        // in the books to answer with — which is the hole a paid FLAG kept getting invented for.
        var payable = await _ledgers.GetOrOpenAccountAsync(ledger.Id, Chart.Payable(ledger.Id), ct);

        await _ledgers.ConvergeAsync(
            Journalize.ExpenseIncurred(ledger.Id, expenseAccount.Id, payable.Id, expense, source), ct: ct);
    }

    public async Task RecordPersonalPaymentAsync(
        Guid expenseId, Guid? fundingAccountId, DateTime valueDate, CancellationToken ct = default)
    {
        var expense = await _expenses.GetByIdAsync(ExpenseId.Create(expenseId), ct);
        if (expense is null || expense.GroupId is not null) return;

        var ledger = await _ledgers.GetOrOpenLedgerAsync(expense.Owner, expense.Amount.Currency, ct);

        var payable = await _ledgers.GetOrOpenAccountAsync(ledger.Id, Chart.Payable(ledger.Id), ct);

        // The expense names its own funding account when one was chosen at entry; the argument wins
        // when somebody settles from somewhere else.
        var declared = fundingAccountId ?? expense.FundingAccountId;
        var funding = declared is { } id
            ? await _ledgers.GetAccountAsync(AccountId.Create(id), ct)
              ?? throw new InvalidOperationException("That funding account is not in this book.")
            : await _ledgers.GetOrOpenAccountAsync(ledger.Id, Chart.Cash(ledger.Id), ct);

        var source = LedgerSources.VendorPayment(expenseId, expense.OccurrenceDate);

        await _ledgers.ConvergeAsync(
            Journalize.VendorPaid(ledger.Id, payable.Id, funding.Id, expense, valueDate, source),
            postOnce: true, ct: ct);
    }

    public async Task ReversePersonalPaymentAsync(Guid expenseId, CancellationToken ct = default)
    {
        var expense = await _expenses.GetByIdAsync(ExpenseId.Create(expenseId), ct);
        if (expense is null || expense.GroupId is not null) return;

        var ledger = await _ledgers.GetLedgerByOwnerAsync(expense.Owner, ct);
        if (ledger is null) return;

        var source = LedgerSources.VendorPayment(expenseId, expense.OccurrenceDate);
        var settled = (await _ledgers.GetEntriesBySourceAsync(ledger.Id, source, ct)).InEffect();
        if (settled.Count == 0) return;

        // Undoing a payment is a new event, so it is dated today rather than restating the day the
        // money moved — the same rule the settlement and vendor-payment reversals already follow.
        foreach (var entry in settled)
            await _ledgers.AddJournalEntryAsync(entry.Reverse(DateTime.UtcNow.Date, reversedByUserId: expense.EnteredBy.Value), ct);

        await _ledgers.CommitAsync(ct);
    }

    public async Task<int> PostDuePersonalExpensesAsync(Guid userId, DateTime asOf, CancellationToken ct = default)
    {
        var due = await _expenses.ListUnpostedPersonalAsync(UserId.Create(userId), asOf.Date, ct);

        var posted = 0;
        foreach (var expense in due)
        {
            await ConvergePersonalExpenseAsync(expense.Id.Value, ct);
            posted++;
        }
        return posted;
    }

    public async Task ConvergeReceiptAsync(Guid receiptId, CancellationToken ct = default)
    {
        var receipt = await _receipts.GetByIdAsync(ReceiptId.Create(receiptId), ct);
        if (receipt is null) return;

        var ledger = await _ledgers.GetOrOpenLedgerAsync(receipt.Owner, receipt.Amount.Currency, ct);
        var inEffect = (await _ledgers.GetEntriesBySourceAsync(ledger.Id, receipt.LedgerSource, ct)).InEffect();

        if (receipt.IsVoid)
        {
            // Money that turned out not to have arrived is unwound where it was recorded, not
            // today: the month it was reported in has to stop claiming it.
            foreach (var stale in ConvergencePlan.Remove(inEffect).Reverse)
                await _ledgers.AddJournalEntryAsync(stale.Reverse(stale.Date), ct);
            if (inEffect.Count > 0) await _ledgers.CommitAsync(ct);
            return;
        }

        var into = await _ledgers.GetAccountAsync(AccountId.Create(receipt.IntoAccountId), ct)
            ?? throw new InvalidOperationException("That account is not in this book.");
        var source = await _ledgers.GetOrOpenAccountAsync(
            ledger.Id, new AccountSpec(
                Chart.IncomeCode(receipt.Source),
                () => Chart.OpenIncomeAccount(ledger.Id, receipt.Source)), ct);

        await _ledgers.ConvergeAsync(Journalize.Received(
            ledger.Id, into.Id, source.Id, receipt.Amount, receipt.ReceivedOn,
            receipt.Source, receipt.LedgerSource, receipt.Owner.Id), ct: ct);
    }

    public async Task ReverseReceiptAsync(Guid receiptId, CancellationToken ct = default)
    {
        var receipt = await _receipts.GetByIdAsync(ReceiptId.Create(receiptId), ct);
        if (receipt is null) return;

        var ledger = await _ledgers.GetLedgerByOwnerAsync(receipt.Owner, ct);
        if (ledger is null) return;

        var inEffect = (await _ledgers.GetEntriesBySourceAsync(ledger.Id, receipt.LedgerSource, ct)).InEffect();
        if (inEffect.Count == 0) return;

        foreach (var stale in inEffect)
            await _ledgers.AddJournalEntryAsync(stale.Reverse(stale.Date), ct);
        await _ledgers.CommitAsync(ct);
    }

    public async Task<Guid> OpenBankAccountAsync(OpenBankAccountCommand cmd, CancellationToken ct = default)
    {
        var ledger = await _ledgers.GetOrOpenLedgerAsync(AccountingEntity.Person(cmd.CallerUserId), cmd.Currency, ct);

        var accountKey = Guid.NewGuid();
        var account = cmd.IsCredit
            ? Chart.OpenDebtAccount(ledger.Id, accountKey, cmd.Name)
            : Chart.OpenCashAccount(ledger.Id, accountKey, cmd.Name);
        await _ledgers.AddAccountAsync(account, ct);

        // A zero balance needs no entry — it would not validate, and an account with no lines
        // already reads as zero. A negative one is a provider quirk we do not try to interpret.
        if (cmd.Balance > 0m)
        {
            var opening = await _ledgers.GetOrOpenAccountAsync(ledger.Id, Chart.OpeningBalance(ledger.Id), ct);

            // Cash is an asset and a card is a liability, so the balance lands on opposite sides:
            // holding £500 debits the account, owing £500 credits it.
            var carriedIn = cmd.IsCredit
                ? Journalize.BalanceCarriedIn(
                    ledger.Id, opening.Id, account.Id,
                    Money.Create(cmd.Balance, cmd.Currency), cmd.AsOf, cmd.Name,
                    $"bank-opening:{account.Id.Value:N}", cmd.CallerUserId)
                : Journalize.BalanceCarriedIn(
                    ledger.Id, account.Id, opening.Id,
                    Money.Create(cmd.Balance, cmd.Currency), cmd.AsOf, cmd.Name,
                    $"bank-opening:{account.Id.Value:N}", cmd.CallerUserId);

            await _ledgers.AddJournalEntryAsync(carriedIn, ct);
        }

        await _ledgers.CommitAsync(ct);
        return account.Id.Value;
    }

    public async Task<Guid> OpenDebtAccountAsync(OpenDebtAccountCommand cmd, CancellationToken ct = default)
    {
        var ledger = await _ledgers.GetOrOpenLedgerAsync(AccountingEntity.Person(cmd.CallerUserId), cmd.Currency, ct);

        var accountKey = Guid.NewGuid();
        var account = Chart.OpenDebtAccount(ledger.Id, accountKey, cmd.Name);
        await _ledgers.AddAccountAsync(account, ct);

        var terms = DebtTerms.For(
            account,
            cmd.AnnualPercentageRate,
            cmd.CreditLimit,
            cmd.StatementDayOfMonth,
            cmd.PaymentDueDayOfMonth,
            cmd.MinimumPayment);
        await _ledgers.AddDebtTermsAsync(terms, ct);

        // Nothing owed yet needs no entry — a zero journalLine would not validate, and an account
        // with no journal_lines already reads as a zero balance.
        if (cmd.OpeningBalance > 0m)
        {
            var opening = await _ledgers.GetOrOpenAccountAsync(ledger.Id, Chart.OpeningBalance(ledger.Id), ct);

            // Not converged: this runs once, when the account is opened, and nothing re-derives it.
            await _ledgers.AddJournalEntryAsync(Journalize.BalanceCarriedIn(
                ledger.Id, opening.Id, account.Id,
                Money.Create(cmd.OpeningBalance, cmd.Currency), cmd.AsOf, cmd.Name,
                $"debt-opening:{account.Id.Value:N}", cmd.CallerUserId), ct);
        }

        await _ledgers.CommitAsync(ct);
        return account.Id.Value;
    }


}
