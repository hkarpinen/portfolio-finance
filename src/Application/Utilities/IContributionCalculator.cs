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
        IReadOnlyList<Expense> personalExpenses,
        IReadOnlyList<(Share Share, Expense Expense)> splits,
        IReadOnlyDictionary<(Guid ShareId, DateTime OccurrenceDate), DateTime> paidShareOccurrences,
        IReadOnlyDictionary<(Guid ExpenseId, DateTime OccurrenceDate), DateTime> paidPersonalBillOccurrences);
}
