using Finance.Domain.ValueObjects;
using Infrastructure.Plaid;

namespace Tests;

/// <summary>
/// The provider's taxonomy is theirs and changes when they say so, which is why it stops at the
/// boundary. A category we have never heard of costs nothing more than a bill somebody has not
/// sorted yet.
/// </summary>
public class BankCategoriesTests
{
    [Theory]
    [InlineData("FOOD_AND_DRINK", ExpenseCategory.Groceries)]
    [InlineData("food_and_drink", ExpenseCategory.Groceries)]
    [InlineData("RENT_AND_UTILITIES", ExpenseCategory.Utilities)]
    [InlineData("MEDICAL", ExpenseCategory.Healthcare)]
    public void AKnownCategory_IsTranslated(string primary, ExpenseCategory expected)
        => Assert.Equal(expected, BankCategories.ToExpenseCategory(primary));

    [Theory]
    [InlineData("SOMETHING_THEY_ADDED_LAST_TUESDAY")]
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElse_IsOther(string? primary)
        => Assert.Equal(ExpenseCategory.Other, BankCategories.ToExpenseCategory(primary));
}
