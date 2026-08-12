using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// The chart for a person's own book. Same numbering as <see cref="GroupChart"/> — 1000s asset,
/// 2000s liability, 3000s equity, 4000s reciprocal, 5000s expense, 6000s income — because the
/// journal and the posting engine do not care whose book it is.
///
/// What differs is which accounts exist and who declares them. A group's accounts are derivable:
/// one pot, one payable, one equity per member. A person's are not — this card, that checking
/// account, each named and carrying terms somebody typed in. So only the two accounts that every
/// book needs are seeded; the rest are opened on demand.
/// </summary>
public static class PersonalChart
{
    public const string CashCode = ChartCodes.Cash;

    /// <summary>Offsets a balance that was carried in rather than posted. Without it the first
    /// card balance has nothing to credit against and the book will not balance.</summary>
    public const string OpeningBalanceCode = ChartCodes.OpeningBalance;

    public static string AssetCode(Guid accountId) => ChartCodes.Asset(accountId);
    public static string LiabilityCode(Guid accountId) => ChartCodes.Liability(accountId);
    public static string ExpenseCode(string category) => ChartCodes.Expense(category);
    public static string IncomeCode(string source) => ChartCodes.Income(source);

    /// <summary>The group's side of this pair is Member:{userId}; the two must agree (P8).</summary>
    public static string DueFromGroupCode(Guid groupId) => ChartCodes.Reciprocal(groupId);

    public static IReadOnlyList<Account> StandardAccounts(LedgerId ledgerId) =>
    [
        Account.Open(ledgerId, CashCode, "Cash", AccountType.Asset),
        Account.Open(ledgerId, OpeningBalanceCode, "Opening balance", AccountType.Equity),
    ];

    public static Account OpenCashAccount(LedgerId ledgerId, Guid accountId, string name) =>
        Account.Open(ledgerId, AssetCode(accountId), name, AccountType.Asset);

    /// <summary>A card or loan. Liability, so a purchase CREDITS it and a payment DEBITS it —
    /// which is why nothing about the posting engine changes for debt.</summary>
    public static Account OpenDebtAccount(LedgerId ledgerId, Guid accountId, string name) =>
        Account.Open(ledgerId, LiabilityCode(accountId), name, AccountType.Liability);

    public static Account OpenExpenseAccount(LedgerId ledgerId, string category) =>
        Account.Open(ledgerId, ExpenseCode(category), $"Expense: {category}", AccountType.Expense);

    public static Account OpenIncomeAccount(LedgerId ledgerId, string source) =>
        Account.Open(ledgerId, IncomeCode(source), $"Income: {source}", AccountType.Income);

    public static Account OpenDueFromGroupAccount(LedgerId ledgerId, Guid groupId) =>
        Account.Open(ledgerId, DueFromGroupCode(groupId), $"Due from group {groupId:N}", AccountType.Asset);
}
