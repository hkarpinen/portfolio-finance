using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// The chart of accounts. One chart, not one per kind of owner: the numbering is
/// <see cref="ChartCodes"/> and the journal does not care whose book it is.
///
/// There used to be a GroupChart and a PersonalChart, which read as two charts and were not —
/// every code in both came from here, and half the members were the same method twice.
///
/// Nothing here asks whose book it is. A ledger records money in and out; what an account MEANS is
/// the same whoever owns it, and which accounts exist follows from what somebody actually did, not
/// from the kind of thing that owns the book. A member bucket only appears where there are members
/// because only there does anybody post to one.
/// </summary>
public static class Chart
{
    public const string CashCode = ChartCodes.Cash;
    public const string PayableCode = ChartCodes.VendorPayable;
    public const string OpeningBalanceCode = ChartCodes.OpeningBalance;

    public static string MemberCode(Guid userId) => ChartCodes.MemberEquity(userId);
    public static string ExpenseCode(string category) => ChartCodes.Expense(category);
    public static string IncomeCode(string source) => ChartCodes.Income(source);
    public static string AssetCode(Guid accountId) => ChartCodes.Asset(accountId);
    public static string LiabilityCode(Guid accountId) => ChartCodes.Liability(accountId);

    /// <summary>Two entities keeping a running position with each other. Both sides use the
    /// counterparty's id, so the pair agrees.</summary>
    public static string DueFromCode(Guid counterpartyId) => ChartCodes.Reciprocal(counterpartyId);

    /// <summary>
    /// What a book starts with. The same three accounts whoever owns it: the ledger does not care.
    /// It records money in and out, and every balance is derived from that history — so an account
    /// nobody has posted to reads as zero and costs nothing to have seeded.
    /// </summary>
    public static IReadOnlyList<Account> StandardAccounts(LedgerId ledgerId) =>
    [
        Account.Open(ledgerId, CashCode, "Cash", AccountType.Asset),
        Account.Open(ledgerId, PayableCode, "Vendor Payable", AccountType.Liability),
        Account.Open(ledgerId, OpeningBalanceCode, "Opening balance", AccountType.Equity),
    ];

    public static AccountSpec Cash(LedgerId ledgerId) =>
        new(CashCode, () => Account.Open(ledgerId, CashCode, "Cash", AccountType.Asset));

    /// <summary>
    /// What is owed to whoever the cost is with, until it is settled. One code for both books on
    /// purpose: a cost is incurred and later funded in either, and one code means one derivation of
    /// "has this been paid".
    /// </summary>
    public static AccountSpec Payable(LedgerId ledgerId) =>
        new(PayableCode, () => Account.Open(ledgerId, PayableCode, "Vendor Payable", AccountType.Liability));

    public static AccountSpec OpeningBalance(LedgerId ledgerId) =>
        new(OpeningBalanceCode, () => Account.Open(ledgerId, OpeningBalanceCode, "Opening balance", AccountType.Equity));

    public static AccountSpec Expense(LedgerId ledgerId, string category) =>
        new(ExpenseCode(category), () => OpenExpenseAccount(ledgerId, category));

    /// <summary>One member's standing in the house's book. Only meaningful where there are
    /// members.</summary>
    public static AccountSpec Member(LedgerId ledgerId, Guid userId) =>
        new(MemberCode(userId), () => OpenMemberAccount(ledgerId, userId));

    /// <summary>
    /// Which account funds a charge. The ledger has no opinion about funding sources and the
    /// engine takes account ids, so this switch is the one place the two meet.
    /// </summary>
    public static AccountSpec Funding(LedgerId ledgerId, FundingSource fundingSource, Guid payerUserId) =>
        fundingSource == FundingSource.GroupCash ? Cash(ledgerId) : Member(ledgerId, payerUserId);

    public static Account OpenMemberAccount(LedgerId ledgerId, Guid userId) =>
        Account.Open(ledgerId, MemberCode(userId), $"Member {userId:N}", AccountType.Equity);

    public static Account OpenExpenseAccount(LedgerId ledgerId, string category) =>
        Account.Open(ledgerId, ExpenseCode(category), $"Expense: {category}", AccountType.Expense);

    public static Account OpenIncomeAccount(LedgerId ledgerId, string source) =>
        Account.Open(ledgerId, IncomeCode(source), $"Income: {source}", AccountType.Income);

    /// <summary>A named account somebody declared — this checking account, that card. Not
    /// derivable the way a pot or a payable is, which is why it carries its own id.</summary>
    public static Account OpenCashAccount(LedgerId ledgerId, Guid accountId, string name) =>
        Account.Open(ledgerId, AssetCode(accountId), name, AccountType.Asset);

    /// <summary>A card or loan. Liability, so a purchase CREDITS it and a payment DEBITS it —
    /// which is why nothing about the posting engine changes for debt.</summary>
    public static Account OpenDebtAccount(LedgerId ledgerId, Guid accountId, string name) =>
        Account.Open(ledgerId, LiabilityCode(accountId), name, AccountType.Liability);

    public static Account OpenDueFromAccount(LedgerId ledgerId, AccountingEntity counterparty) =>
        Account.Open(ledgerId, DueFromCode(counterparty.Id), $"Due from {counterparty}", AccountType.Asset);
}
