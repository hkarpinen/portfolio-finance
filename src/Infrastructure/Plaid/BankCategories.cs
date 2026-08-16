using Finance.Domain.ValueObjects;

namespace Infrastructure.Plaid;

/// <summary>
/// The provider's word for what a transaction was, in ours.
///
/// Their taxonomy is theirs and changes when they say so, which is exactly why it does not reach
/// the domain: an expense is filed under one of our categories or under Other, and a category we
/// have never heard of costs nothing more than an expense somebody has not sorted yet.
/// </summary>
internal static class BankCategories
{
    private static readonly Dictionary<string, ExpenseCategory> ByPrimary = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RENT_AND_UTILITIES"] = ExpenseCategory.Utilities,
        ["FOOD_AND_DRINK"] = ExpenseCategory.Groceries,
        ["TRANSPORTATION"] = ExpenseCategory.Transportation,
        ["TRAVEL"] = ExpenseCategory.Transportation,
        ["ENTERTAINMENT"] = ExpenseCategory.Entertainment,
        ["MEDICAL"] = ExpenseCategory.Healthcare,
        ["GENERAL_SERVICES"] = ExpenseCategory.Subscriptions,
        ["PERSONAL_CARE"] = ExpenseCategory.Other,
        ["GENERAL_MERCHANDISE"] = ExpenseCategory.Other,
        ["HOME_IMPROVEMENT"] = ExpenseCategory.Other,
        ["LOAN_PAYMENTS"] = ExpenseCategory.Other,
        ["BANK_FEES"] = ExpenseCategory.Other,
    };

    /// <summary>Other when the provider says nothing, or says something we do not carry.</summary>
    public static ExpenseCategory ToExpenseCategory(string? primaryCategory) =>
        primaryCategory is not null && ByPrimary.TryGetValue(primaryCategory, out var category)
            ? category
            : ExpenseCategory.Other;
}
