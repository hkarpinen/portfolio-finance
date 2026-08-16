using Client.Validators;
using Finance.Application.Commands;
using Finance.Domain.ValueObjects;

namespace Tests;

/// <summary>
/// The Category stays a free string on the wire (API compatibility) but must name a real
/// ExpenseCategory, case-insensitively. One parser, and an unknown category is rejected rather
/// than quietly filed under Other — which would lose what the person meant.
/// </summary>
public class ExpenseCategoryValidationTests
{
    private static CreateExpenseCommand Cmd(string category) => new(
        CallerUserId: Guid.NewGuid(), Title: "Gym", Amount: 50m, Currency: "USD",
        Category: category, DueDate: DateTime.UtcNow.Date);

    [Theory]
    [InlineData("Rent")]
    [InlineData("rent")]      // case-insensitive
    [InlineData("OTHER")]
    public void ValidCategory_Passes(string category) =>
        Assert.True(new CreateExpenseRequestValidator().Validate(Cmd(category)).IsValid);

    [Theory]
    [InlineData("Bananas")]
    [InlineData("")]
    public void BogusOrEmptyCategory_Fails(string category) =>
        Assert.False(new CreateExpenseRequestValidator().Validate(Cmd(category)).IsValid);

    // Both readings of a category go through ExpenseCategories now: the validator asks IsKnown at
    // the edge, and Parse throws beyond it, so an unknown one can never be silently re-filed.
    [Theory]
    [InlineData("Groceries", ExpenseCategory.Groceries)]
    [InlineData("groceries", ExpenseCategory.Groceries)]
    [InlineData("RENT", ExpenseCategory.Rent)]
    public void AKnownCategory_ParsesWhateverItsCasing(string input, ExpenseCategory expected)
    {
        Assert.True(ExpenseCategories.IsKnown(input));
        Assert.Equal(expected, ExpenseCategories.Parse(input));
    }

    [Theory]
    [InlineData("Groceriez")]
    [InlineData("")]
    [InlineData(null)]
    public void AnUnknownCategory_IsRefusedRatherThanFiledUnderOther(string? input)
    {
        Assert.False(ExpenseCategories.IsKnown(input));
        Assert.Throws<ArgumentException>(() => ExpenseCategories.Parse(input));
    }
}
