using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Engines;

/// <summary>
/// What each thing that happens MEANS in debits and credits.
///
/// These used to be written inline in the manager, between the account lookups and the repository
/// calls, and two of them were written twice — a cost incurred and a payable cleared each had a
/// household version and a personal one that had to agree by memory. Here they are named once, and
/// the household and personal paths call the same rule.
///
/// Which accounts to use is the caller's job: resolving "the groceries account for this ledger"
/// is a database round trip, so it cannot live in here. What these own is the DIRECTION — which
/// side each account takes, and what the entry is called.
/// </summary>
public static class Journalize
{
    /// <summary>
    /// A cost incurred and owed to whoever it is with. Not yet funded: who pays, and out of what,
    /// is a separate fact recorded later, which is what lets "has this been paid" be a question
    /// the books answer instead of a flag somebody sets.
    ///
    /// The expense states its own amount, title, period, id and author — five arguments that were
    /// being unpacked at the call site and handed straight back.
    /// </summary>
    public static JournalEntry ExpenseIncurred(
        LedgerId ledger, AccountId category, AccountId payable, Expense expense, string source) =>
        ExpenseIncurred(
            ledger, category, payable, expense.Amount, expense.OccurrenceDate,
            expense.Title, source, expense.Id.Value, expense.EnteredBy.Value);

    /// <summary>For the household path, which holds a command rather than the expense.</summary>
    public static JournalEntry ExpenseIncurred(
        LedgerId ledger, AccountId category, AccountId payable, Money amount,
        DateTime occurredOn, string title, string source, Guid expenseId, Guid? enteredBy) =>
        JournalEntry.Movement(
            ledger, debit: category, credit: payable, amount, occurredOn,
            $"{title} — incurred", source,
            sourceExpenseId: expenseId, postedByUserId: enteredBy);

    /// <summary>
    /// One member taking on their part of a shared cost — it moves off the nominal expense account
    /// and onto that member's standing. Keyed per share, so a split changed later is corrected on
    /// its own without disturbing the cost it belongs to.
    /// </summary>
    public static JournalEntry ShareBorne(
        LedgerId ledger, AccountId member, AccountId category, Money amount,
        DateTime on, string source, Guid expenseId, Guid memberUserId) =>
        JournalEntry.Movement(
            ledger, debit: member, credit: category, amount, on,
            "Share", source,
            sourceExpenseId: expenseId, sourceMemberId: memberUserId, postedByUserId: memberUserId);

    /// <summary>
    /// The debt to the outside world settled, out of whatever funded it — a member's own standing
    /// when they fronted it, the pot when it was pooled, a card in somebody's own book. The engine
    /// has no opinion about which; it is told the account.
    ///
    /// Where the caller holds the expense, it states its own cost, period, name and author.
    /// </summary>
    public static JournalEntry VendorPaid(
        LedgerId ledger, AccountId payable, AccountId funding, Expense expense,
        DateTime paidOn, string source) =>
        VendorPaid(
            ledger, payable, funding, expense.Amount, paidOn, $"{expense.Title} — paid", source,
            expense.Id.Value, expense.OccurrenceDate, expense.EnteredBy.Value);

    /// <summary>For the household path, which holds a command rather than the expense.</summary>
    public static JournalEntry VendorPaid(
        LedgerId ledger, AccountId payable, AccountId funding, Money amount,
        DateTime paidOn, string description, string source,
        Guid expenseId, DateTime occurrence, Guid? paidBy) =>
        JournalEntry.Movement(
            ledger, debit: payable, credit: funding, amount, paidOn,
            description, source,
            sourceExpenseId: expenseId, sourceOccurrence: occurrence, postedByUserId: paidBy);

    /// <summary>
    /// A debtor squaring up their share into whichever account paid the vendor, so the settlement
    /// always mirrors how its expense was funded.
    /// </summary>
    public static JournalEntry Settlement(
        LedgerId ledger, AccountId funding, AccountId debtor, Money amount,
        DateTime valueDate, string source,
        Guid expenseId, Guid shareId, DateTime occurrence, Guid fromUserId) =>
        JournalEntry.Movement(
            ledger, debit: funding, credit: debtor, amount, valueDate,
            "Settlement", source,
            sourceExpenseId: expenseId, sourceShareId: shareId,
            sourceOccurrence: occurrence, sourceMemberId: fromUserId, postedByUserId: fromUserId);

    /// <summary>
    /// Two people settling up directly, no pot involved — both standings move toward zero. Not tied
    /// to any one cost, which is why it carries no expense.
    /// </summary>
    public static JournalEntry SettleUp(
        LedgerId ledger, AccountId creditor, AccountId debtor, Money amount,
        DateTime on, string source, Guid fromUserId) =>
        JournalEntry.Movement(
            ledger, debit: creditor, credit: debtor, amount, on,
            "Settle-up", source,
            sourceMemberId: fromUserId, postedByUserId: fromUserId);

    /// <summary>
    /// A balance that was carried in rather than posted. Without the offset the first card balance
    /// has nothing to credit against and the book does not balance from its first entry.
    /// </summary>
    public static JournalEntry BalanceCarriedIn(
        LedgerId ledger, AccountId openingBalance, AccountId account, Money amount,
        DateTime asOf, string name, string source, Guid? enteredBy) =>
        JournalEntry.Movement(
            ledger, debit: openingBalance, credit: account, amount, asOf,
            $"{name} — balance carried in", source, postedByUserId: enteredBy);
}
