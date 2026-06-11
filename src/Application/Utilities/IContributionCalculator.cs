using Finance.Application.Dtos;
using Finance.Domain.Aggregates;

namespace Finance.Application.Utilities;

public interface IContributionCalculator
{
    IReadOnlyCollection<ContributionPeriodSummaryDto> BuildSummaries(
        DateTime now,
        int monthCount,
        int pastMonths,
        IReadOnlyList<IncomeSource> incomeSources,
        IReadOnlyList<Charge> personalCharges,
        IReadOnlyList<(Allocation Allocation, Charge Charge)> splits,
        IReadOnlyDictionary<(Guid AllocationId, DateTime OccurrenceDate), DateTime> paidAllocationOccurrences,
        IReadOnlyDictionary<(Guid ChargeId, DateTime OccurrenceDate), DateTime> paidPersonalBillOccurrences);
}
