using Finance.Domain.Engines;
using Finance.Domain.ValueObjects;

namespace Tests;

public class HouseholdCoverageEngineTests
{
    private static Money Usd(decimal amount) => Money.Create(amount, "USD");
    private static readonly DateTime PeriodStart = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PeriodEnd = new(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void BuildCoverageStatus_IncomeExceedsBills_ReturnsCovered()
    {
        var status = HouseholdCoverageEngine.BuildCoverageStatus(
            Guid.NewGuid(), Usd(5000m), Usd(4000m), Usd(3000m), PeriodStart, PeriodEnd);

        Assert.Equal("Covered", status.Status);
        Assert.True(status.IsFullyCovered);
    }

    [Fact]
    public void BuildCoverageStatus_IncomeEqualsBills_ReturnsCovered()
    {
        var status = HouseholdCoverageEngine.BuildCoverageStatus(
            Guid.NewGuid(), Usd(3000m), Usd(3000m), Usd(3000m), PeriodStart, PeriodEnd);

        Assert.Equal("Covered", status.Status);
        Assert.True(status.IsFullyCovered);
    }

    [Fact]
    public void BuildCoverageStatus_RatioAbove80Percent_ReturnsAtRisk()
    {
        // net = 2500, bills = 3000 → ratio = 0.8333 ≥ 0.8 → AtRisk
        var status = HouseholdCoverageEngine.BuildCoverageStatus(
            Guid.NewGuid(), Usd(3000m), Usd(2500m), Usd(3000m), PeriodStart, PeriodEnd);

        Assert.Equal("AtRisk", status.Status);
        Assert.False(status.IsFullyCovered);
    }

    [Fact]
    public void BuildCoverageStatus_RatioBelow80Percent_ReturnsOvercommitted()
    {
        // net = 1000, bills = 3000 → ratio = 0.333 < 0.8 → Overcommitted
        var status = HouseholdCoverageEngine.BuildCoverageStatus(
            Guid.NewGuid(), Usd(3000m), Usd(1000m), Usd(3000m), PeriodStart, PeriodEnd);

        Assert.Equal("Overcommitted", status.Status);
        Assert.False(status.IsFullyCovered);
    }

    [Fact]
    public void BuildCoverageStatus_ZeroBills_ReturnsCovered()
    {
        var status = HouseholdCoverageEngine.BuildCoverageStatus(
            Guid.NewGuid(), Usd(5000m), Usd(5000m), Usd(0m), PeriodStart, PeriodEnd);

        Assert.Equal("Covered", status.Status);
        Assert.Equal(1m, status.Ratio);
    }

    [Fact]
    public void BuildCoverageStatus_SetsAmountsCorrectly()
    {
        var id = Guid.NewGuid();
        var status = HouseholdCoverageEngine.BuildCoverageStatus(
            id, Usd(5000m), Usd(4500m), Usd(3000m), PeriodStart, PeriodEnd);

        Assert.Equal(id, status.HouseholdId);
        Assert.Equal(5000m, status.TotalGrossIncomeAmount);
        Assert.Equal(4500m, status.TotalNetIncomeAmount);
        Assert.Equal(3000m, status.TotalBillsAmount);
        Assert.Equal(PeriodStart, status.PeriodStart);
        Assert.Equal(PeriodEnd, status.PeriodEnd);
    }
}
