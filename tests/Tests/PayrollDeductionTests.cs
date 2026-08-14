using Finance.Domain.ValueObjects;

namespace Tests;

/// <summary>
/// A deduction knows how much it takes. Whether that is a slice of gross or a flat amount at its
/// own cadence was branched on twice in the payroll engine, each time reading Method, Value and
/// Frequency off the deduction — two copies of one rule, able to drift apart.
/// </summary>
public class PayrollDeductionTests
{
    private static PayrollDeduction Percent(decimal percent) => PayrollDeduction.Create(
        DeductionType.Retirement401k, "401k", DeductionCalculationMethod.PercentOfGross,
        percent, frequency: RecurrenceFrequency.Monthly);

    private static PayrollDeduction Flat(decimal amount, RecurrenceFrequency every) =>
        PayrollDeduction.Create(
            DeductionType.HealthInsurance, "Health", DeductionCalculationMethod.FixedAmount,
            amount, frequency: every);

    [Fact]
    public void APercentage_IsTakenOfTheMonthsGross()
        => Assert.Equal(300m, Percent(6m).MonthlyAmount(5_000m));

    [Fact]
    public void AFlatMonthlyAmount_IsItself()
        => Assert.Equal(120m, Flat(120m, RecurrenceFrequency.Monthly).MonthlyAmount(5_000m));

    // A flat amount quoted at another cadence is converted, and does not care what gross is.
    [Fact]
    public void AFlatWeeklyAmount_IsConvertedToTheMonth()
    {
        var weekly = Flat(30m, RecurrenceFrequency.Weekly);

        Assert.Equal(weekly.MonthlyAmount(5_000m), weekly.MonthlyAmount(9_000m));
        Assert.Equal(130m, weekly.MonthlyAmount(5_000m));
    }

    // Pre-tax by its kind, or by somebody marking it exempt — one question, either way.
    [Fact]
    public void ARetirementContribution_ReducesTaxableIncome()
        => Assert.True(Percent(6m).ReducesTaxableIncome);
}
