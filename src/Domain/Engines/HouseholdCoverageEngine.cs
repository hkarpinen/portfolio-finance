using Finance.Domain.ValueObjects;

namespace Finance.Domain.Engines;

/// <summary>
/// Computes household income-to-bills coverage status.
/// Change driver: coverage threshold product decisions.
/// </summary>
public static class HouseholdCoverageEngine
{
    /// <summary>
    /// Income-to-bills ratio below which a household is considered "AtRisk" rather than "Covered".
    /// Below half this threshold the household is "Overcommitted".
    /// </summary>
    private const decimal AtRiskThreshold = 0.8m;

    public static CoverageStatus BuildCoverageStatus(
        Guid householdId,
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
            householdId,
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
