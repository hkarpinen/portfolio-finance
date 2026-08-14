using Client.Validators;
using Finance.Application.Commands;

namespace Tests;

/// <summary>
/// The personal-charge Category stays a free string on the wire (API compatibility) but must name a
/// real ChargeCategory (case-insensitive) — the manager already coerces unknowns to Other, so this
/// only rejects genuinely bogus input rather than silently swallowing it.
/// </summary>
public class ChargeCategoryValidationTests
{
    private static CreateChargeCommand Cmd(string category) => new(
        CallerUserId: Guid.NewGuid(), Title: "Gym", Amount: 50m, Currency: "USD",
        Category: category, DueDate: DateTime.UtcNow.Date);

    [Theory]
    [InlineData("Rent")]
    [InlineData("rent")]      // case-insensitive
    [InlineData("OTHER")]
    public void ValidCategory_Passes(string category) =>
        Assert.True(new CreateChargeRequestValidator().Validate(Cmd(category)).IsValid);

    [Theory]
    [InlineData("Bananas")]
    [InlineData("")]
    public void BogusOrEmptyCategory_Fails(string category) =>
        Assert.False(new CreateChargeRequestValidator().Validate(Cmd(category)).IsValid);
}
