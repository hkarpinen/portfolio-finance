using Finance.Application.Dtos;
using Finance.Domain.ReadModels;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Queries;

public interface IExpenseSplitQuery
{
    Task<IReadOnlyCollection<SplitWithSharedExpenseDetail>> ListByUserWithBillDetailsAsync(
        UserId userId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns per-month, per-member contribution summaries for all household expenses.
    /// Recurring expenses are projected forward/back across the window.
    /// </summary>
    Task<IReadOnlyCollection<HouseholdMonthlyContributionsDto>> ListByHouseholdAsync(
        HouseholdId householdId, DateTime windowStart, DateTime windowEnd, CancellationToken cancellationToken = default);

    /// <summary>
    /// For the given caller, returns payment status keyed by expenseId.
    /// Expenses for which the caller has no split are omitted.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, CallerSplitStatusDto>> GetCallerPaymentStatusAsync(
        UserId callerId,
        IReadOnlyCollection<(Guid ExpenseId, DateTime OccurrenceDate)> expenseOccurrences,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the set of SplitIds that have a payment record for the specified expense occurrence.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetPaidSplitIdsForExpenseAsync(
        Guid expenseId, DateTime occurrenceDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all (SplitId, OccurrenceDate) pairs that the user has paid within the date window,
    /// mapped to the timestamp the payment was recorded.
    /// </summary>
    Task<IReadOnlyDictionary<(Guid SplitId, DateTime OccurrenceDate), DateTime>> GetPaidOccurrencesAsync(
        UserId userId, DateTime from, DateTime to, CancellationToken cancellationToken = default);
}
