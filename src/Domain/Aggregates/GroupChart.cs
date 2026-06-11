using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// The standard chart of accounts for a <em>group</em> ledger — domain structural
/// knowledge (which accounts a group's book has, and their types and codes). Codes
/// follow classic numbering: 1000s assets, 2000s liabilities, 3000s equity, 5000s
/// expense. This is the domain's, not Resource Access's, knowledge — Resource Access
/// only persists the accounts these factories construct.
/// </summary>
public static class GroupChart
{
    public const string CashCode = "1000";
    public const string VendorPayableCode = "2000";

    public static string MemberCode(Guid userId) => $"3000:{userId:N}";
    public static string ExpenseCode(string category) => $"5000:{category.ToLowerInvariant()}";

    /// <summary>The accounts every group ledger is opened with.</summary>
    public static IReadOnlyList<Account> StandardAccounts(LedgerId ledgerId) =>
    [
        OpenCashAccount(ledgerId),
        Account.Open(ledgerId, VendorPayableCode, "Vendor Payable", AccountType.Liability),
    ];

    /// <summary>The shared Cash (pool) account — the funding account for pooled charges.</summary>
    public static Account OpenCashAccount(LedgerId ledgerId) =>
        Account.Open(ledgerId, CashCode, "Cash", AccountType.Asset);

    /// <summary>A member's equity account (their stake in the household's book).</summary>
    public static Account OpenMemberAccount(LedgerId ledgerId, Guid userId) =>
        Account.Open(ledgerId, MemberCode(userId), $"Member {userId:N}", AccountType.Equity);

    /// <summary>A category expense account (nominal — feeds the P&amp;L).</summary>
    public static Account OpenExpenseAccount(LedgerId ledgerId, string category) =>
        Account.Open(ledgerId, ExpenseCode(category), $"Expense: {category}", AccountType.Expense);
}
