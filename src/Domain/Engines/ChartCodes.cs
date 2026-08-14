namespace Finance.Domain.Engines;

/// <summary>
/// The account-numbering convention, shared by every ledger because the journal does not care
/// whose book it is: 1000s asset, 2000s liability, 3000s equity, 4000s reciprocal, 5000s expense,
/// 6000s income.
///
/// What differs between a group's book and a person's is only WHICH accounts exist — a group's are
/// derivable (one pot, one payable, one equity per member), a person's are declared. So the codes
/// live here once and the two charts differ only in what they seed.
/// </summary>
public static class ChartCodes
{
    public const string Cash = "1000";
    public const string VendorPayable = "2000";
    public const string OpeningBalance = "3900";

    public static string Asset(Guid accountId) => $"1000:{accountId:N}";
    public static string Liability(Guid accountId) => $"2000:{accountId:N}";

    /// <summary>A named debt somebody declared — a card or a loan, which carries terms. The
    /// shared payable is 2000 with no subject and is not one: nobody quotes you a rate on it.</summary>
    public static bool IsDeclaredDebt(string code) => code.StartsWith("2000:", StringComparison.Ordinal);
    public static string MemberEquity(Guid userId) => $"3000:{userId:N}";
    public static string Reciprocal(Guid counterpartyId) => $"4000:{counterpartyId:N}";
    public static string Expense(string category) => $"5000:{category.ToLowerInvariant()}";
    public static string Income(string source) => $"6000:{source.ToLowerInvariant()}";
}
