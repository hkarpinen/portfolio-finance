using Finance.Application.Contracts;
using Finance.Application.Managers.Dependencies;
using Finance.Domain.ValueObjects;

namespace Infrastructure.Engines;

internal sealed class HouseholdCoverageEngine : IHouseholdCoverageEngine
{
    /// <summary>
    /// Income-to-bills ratio below which a household is considered "AtRisk" rather than "Covered".
    /// Below half this threshold the household is "Overcommitted".
    /// </summary>
    private const decimal AtRiskThreshold = 0.8m;

    public CoverageStatusResponse BuildCoverageStatus(
        Guid householdId,
        Money totalIncome,
        Money totalBills,
        DateTime periodStart,
        DateTime periodEnd)
    {
        var ratio = totalBills.Amount == 0
            ? 1m
            : Math.Round(totalIncome.Amount / totalBills.Amount, 4);

        var isFullyCovered = totalIncome.Amount >= totalBills.Amount;
        var status = isFullyCovered
            ? "Covered"
            : ratio >= AtRiskThreshold ? "AtRisk" : "Overcommitted";

        return new CoverageStatusResponse(
            householdId,
            totalIncome.Amount,
            totalBills.Amount,
            ratio,
            isFullyCovered,
            status,
            periodStart,
            periodEnd);
    }
}
