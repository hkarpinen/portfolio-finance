using Finance.Domain.ValueObjects;

namespace Finance.Domain.Engines;

public interface IGroupCoverageEngine
{
    CoverageStatus BuildCoverageStatus(
        Guid groupId,
        Money totalGrossIncome,
        Money totalNetIncome,
        Money totalBills,
        DateTime periodStart,
        DateTime periodEnd);
}

internal sealed class GroupCoverageEngine : IGroupCoverageEngine
{
    // Income-to-bills ratio below which a household is "AtRisk" rather than "Covered". Below HALF
    // this threshold it is "Overcommitted".
    private const decimal AtRiskThreshold = 0.8m;

    public CoverageStatus BuildCoverageStatus(
        Guid groupId,
        Money totalGrossIncome,
        Money totalNetIncome,
        Money totalBills,
        DateTime periodStart,
        DateTime periodEnd)
    {
        var ratio = totalBills.Amount == 0
            ? 1m
            : Math.Round(totalNetIncome.Amount / totalBills.Amount, 4);

        var isFullyCovered = totalNetIncome.Amount >= totalBills.Amount;
        var status = isFullyCovered
            ? "Covered"
            : ratio >= AtRiskThreshold ? "AtRisk" : "Overcommitted";

        return new CoverageStatus(
            groupId,
            totalGrossIncome.Amount,
            totalNetIncome.Amount,
            totalBills.Amount,
            ratio,
            isFullyCovered,
            status,
            periodStart,
            periodEnd);
    }
}
