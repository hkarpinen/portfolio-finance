using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

// Codes follow classic numbering: 1000s assets, 2000s liabilities, 3000s equity, 5000s expense.
public static class GroupChart
{
    public const string CashCode = "1000";
    public const string VendorPayableCode = "2000";

    public static string MemberCode(Guid userId) => $"3000:{userId:N}";
    public static string ExpenseCode(string category) => $"5000:{category.ToLowerInvariant()}";

    public static IReadOnlyList<Account> StandardAccounts(LedgerId ledgerId) =>
    [
        OpenCashAccount(ledgerId),
        Account.Open(ledgerId, VendorPayableCode, "Vendor Payable", AccountType.Liability),
    ];

    // The funding account for pooled charges.
    public static Account OpenCashAccount(LedgerId ledgerId) =>
        Account.Open(ledgerId, CashCode, "Cash", AccountType.Asset);

    public static Account OpenMemberAccount(LedgerId ledgerId, Guid userId) =>
        Account.Open(ledgerId, MemberCode(userId), $"Member {userId:N}", AccountType.Equity);

    public static Account OpenExpenseAccount(LedgerId ledgerId, string category) =>
        Account.Open(ledgerId, ExpenseCode(category), $"Expense: {category}", AccountType.Expense);
}
