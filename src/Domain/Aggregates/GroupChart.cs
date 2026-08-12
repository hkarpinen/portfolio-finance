using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

// What a GROUP's book seeds and how its accounts are named. The numbering itself is shared —
// see ChartCodes — because a journal does not care whose book it is.
public static class GroupChart
{
    public const string CashCode = ChartCodes.Cash;
    public const string VendorPayableCode = ChartCodes.VendorPayable;

    public static string MemberCode(Guid userId) => ChartCodes.MemberEquity(userId);
    public static string ExpenseCode(string category) => ChartCodes.Expense(category);

    public static IReadOnlyList<Account> StandardAccounts(LedgerId ledgerId) =>
    [
        OpenCashAccount(ledgerId),
        Account.Open(ledgerId, VendorPayableCode, "Vendor Payable", AccountType.Liability),
    ];

    // The funding account for pooled charges.
    public static Account OpenCashAccount(LedgerId ledgerId) =>
        Account.Open(ledgerId, CashCode, "Cash", AccountType.Asset);

    public static AccountSpec Cash(LedgerId ledgerId) =>
        new(CashCode, () => OpenCashAccount(ledgerId));

    public static AccountSpec VendorPayable(LedgerId ledgerId) =>
        new(VendorPayableCode, () => Account.Open(ledgerId, VendorPayableCode, "Vendor Payable", AccountType.Liability));

    public static AccountSpec Member(LedgerId ledgerId, Guid userId) =>
        new(MemberCode(userId), () => OpenMemberAccount(ledgerId, userId));

    /// <summary>
    /// Which account funds a charge. The ledger has no opinion about funding sources and the
    /// engine takes account ids, so this switch is the one place the two meet.
    /// </summary>
    public static AccountSpec Funding(LedgerId ledgerId, FundingSource fundingSource, Guid payerUserId) =>
        fundingSource == FundingSource.GroupCash ? Cash(ledgerId) : Member(ledgerId, payerUserId);

    public static AccountSpec Expense(LedgerId ledgerId, string category) =>
        new(ExpenseCode(category), () => OpenExpenseAccount(ledgerId, category));

    public static Account OpenMemberAccount(LedgerId ledgerId, Guid userId) =>
        Account.Open(ledgerId, MemberCode(userId), $"Member {userId:N}", AccountType.Equity);

    public static Account OpenExpenseAccount(LedgerId ledgerId, string category) =>
        Account.Open(ledgerId, ExpenseCode(category), $"Expense: {category}", AccountType.Expense);
}
