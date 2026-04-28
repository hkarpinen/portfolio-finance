using Bills.Application.Contracts;
using Bills.Domain.ValueObjects;

namespace Bills.Application.Queries;

public interface IBillSplitQuery
{
    Task<IReadOnlyCollection<SplitWithBillDetail>> ListByUserWithBillDetailsAsync(
        UserId userId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns per-month, per-member contribution summaries for all bills in a household.
    /// Recurring bills are projected forward/back across the 12-month window.
    /// </summary>
    Task<IReadOnlyCollection<HouseholdMonthlyContributions>> ListByHouseholdAsync(
        HouseholdId householdId,
        DateTime windowStart,
        DateTime windowEnd,
        CancellationToken cancellationToken = default);
}
