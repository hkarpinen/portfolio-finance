using Finance.Domain.Utilities;
using Finance.Domain.ValueObjects;

namespace Tests;

public class UserBudgetCalculatorTests
{
    // ── MonthlyEquivalent ────────────────────────────────────────────────────

    [Theory]
    [InlineData(1200, RecurrenceFrequency.Monthly, 1200)]
    [InlineData(600, RecurrenceFrequency.BiWeekly, 1300)]      // 600 × 26/12
    [InlineData(52, RecurrenceFrequency.Weekly, 225)]           // 52 × 52/12 ≈ 225.33
    [InlineData(14400, RecurrenceFrequency.Annually, 1200)]
    [InlineData(3600, RecurrenceFrequency.Quarterly, 1200)]
    public void MonthlyEquivalent_ReturnsExpected(decimal amount, RecurrenceFrequency freq, decimal expected)
    {
        var result = UserBudgetCalculator.MonthlyEquivalent(amount, freq);
        Assert.Equal(expected, Math.Round(result, 0));
    }

    // ── PerPaycheckAmount ────────────────────────────────────────────────────

    [Fact]
    public void PerPaycheckAmount_AnnualSalaryBiWeekly_Returns26thOfAnnual()
    {
        // $80,000/year, paid bi-weekly → $80,000 / 26
        var result = UserBudgetCalculator.PerPaycheckAmount(80_000m, RecurrenceFrequency.Annually, RecurrenceFrequency.BiWeekly);
        Assert.Equal(Math.Round(80_000m / 26m, 10), Math.Round(result, 10));
    }

    [Fact]
    public void PerPaycheckAmount_MonthlySalaryMonthly_ReturnsSameAmount()
    {
        var result = UserBudgetCalculator.PerPaycheckAmount(5000m, RecurrenceFrequency.Monthly, RecurrenceFrequency.Monthly);
        Assert.Equal(5000m, result);
    }

    [Fact]
    public void PerPaycheckAmount_AnnualSalaryMonthly_ReturnsAnnualDividedBy12()
    {
        var result = UserBudgetCalculator.PerPaycheckAmount(60_000m, RecurrenceFrequency.Annually, RecurrenceFrequency.Monthly);
        Assert.Equal(5000m, result);
    }

    // ── AnnualAmount ─────────────────────────────────────────────────────────

    [Fact]
    public void AnnualAmount_Monthly_MultipliesBy12()
    {
        Assert.Equal(60_000m, UserBudgetCalculator.AnnualAmount(5000m, RecurrenceFrequency.Monthly));
    }

    [Fact]
    public void AnnualAmount_Annually_ReturnsSameAmount()
    {
        Assert.Equal(80_000m, UserBudgetCalculator.AnnualAmount(80_000m, RecurrenceFrequency.Annually));
    }
}
