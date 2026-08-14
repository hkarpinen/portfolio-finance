using Finance.Application.Commands;
using Finance.Application.Dtos;
using Finance.Domain.ValueObjects;

namespace Tests;

/// <summary>
/// Three endpoints took only an income id and mutated whatever came back. Anyone signed in could
/// set the tax profile on somebody else's income, or add and remove its deductions, by knowing the
/// id — the sibling endpoints on the same manager had the check and these did not.
///
/// These assert the shape that made that possible is gone: every one of them now carries who is
/// asking, so a controller that forgets to fill it in fails the check rather than skipping it.
/// </summary>
public class IncomeOwnershipTests
{
    [Fact]
    public void SettingATaxProfile_AsksWhoIsRequestingIt()
    {
        var cmd = new SetTaxProfileCommand(Guid.NewGuid(), null);

        // Unset is Guid.Empty, which matches no owner — a forgotten controller denies rather
        // than admits.
        Assert.Equal(Guid.Empty, cmd.CallerUserId);
    }

    [Fact]
    public void AddingADeduction_AsksWhoIsRequestingIt()
    {
        var deduction = new PayrollDeductionDto(
            DeductionType.Retirement401k, "401k", DeductionCalculationMethod.PercentOfGross,
            6m, false, RecurrenceFrequency.Monthly, false);

        Assert.Equal(Guid.Empty, new AddDeductionCommand(Guid.NewGuid(), deduction).CallerUserId);
    }

    [Fact]
    public void RemovingADeduction_AsksWhoIsRequestingIt()
        => Assert.Equal(
            Guid.Empty,
            new RemoveDeductionCommand(Guid.NewGuid(), "Retirement401k", "401k").CallerUserId);
}
