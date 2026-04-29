using Finance.Application.Contracts;
using Finance.Application.Managers.Dependencies;
using Finance.Domain.ValueObjects;

namespace Infrastructure.Engines;

internal sealed class HouseholdCoverageEngine : IHouseholdCoverageEngine
{
    public CoverageStatusResponse BuildCoverageStatus(
        Guid householdId,
        Money totalIncome,
        Money totalBills,
        DateTime periodStart,
        DateTime periodEnd)
    {
        var ratio = totalIncome.Amount == 0
            ? 0m
            : Math.Round(totalIncome.Amount / totalBills.Amount, 4);

        var isFullyCovered = totalIncome.Amount >= totalBills.Amount;
        var status = isFullyCovered
            ? "Covered"
            : totalIncome.Amount >= totalBills.Amount * 0.8m ? "AtRisk" : "Overcommitted";

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
