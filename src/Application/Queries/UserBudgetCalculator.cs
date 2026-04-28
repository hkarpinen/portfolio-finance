using Bills.Application.Contracts;
using Bills.Domain.ValueObjects;

namespace Bills.Application.Queries;

/// <summary>
/// Shared helpers for computing a user's personal budget figures (income and obligations).
/// Used by both the overview endpoint and the per-household detail endpoint so "Net Balance"
/// is consistent everywhere: a user's own monthly income minus the sum of their split
/// obligations across ALL households.
/// </summary>
public static class UserBudgetCalculator
{
    /// <summary>
    /// Returns the monthly-equivalent amount of an income source, normalised so Weekly/BiWeekly/Quarterly/etc.
    /// all map onto a per-month figure suitable for budgeting.
    /// </summary>
    public static decimal MonthlyEquivalent(IncomeResponse src) =>
        MonthlyEquivalent(src.Amount, src.Frequency);

    /// <summary>
    /// Returns the monthly-equivalent of a raw amount + frequency combination.
    /// This overload is the canonical computation; the <see cref="MonthlyEquivalent(IncomeResponse)"/>
    /// overload delegates here so the switch table is defined exactly once.
    /// </summary>
    public static decimal MonthlyEquivalent(decimal amount, RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Weekly       => amount * 52m / 12m,
        RecurrenceFrequency.BiWeekly     => amount * 26m / 12m,
        RecurrenceFrequency.Annually     => amount / 12m,
        RecurrenceFrequency.Quarterly    => amount / 3m,
        RecurrenceFrequency.SemiAnnually => amount / 6m,
        _                                => amount,
    };

    /// <summary>
    /// Sum of all active income sources the user has, normalised to a monthly figure.
    /// Only sources that are active for the given (year, month) are included.
    /// </summary>
    public static decimal MonthlyIncomeForUser(IEnumerable<IncomeResponse> incomeSources, int year, int month)
    {
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        decimal total = 0m;
        foreach (var src in incomeSources)
        {
            if (!src.IsActive) continue;
            if (src.StartDate > monthEnd) continue;
            if (src.EndDate.HasValue && src.EndDate.Value < monthStart) continue;
            total += MonthlyEquivalent(src);
        }
        return total;
    }

    /// <summary>
    /// Sum of the user's split obligations for a specific calendar month, across ALL households.
    /// Recurring bills are projected into the month via <see cref="RecurrenceSchedule.GetOccurrencesInRange"/>;
    /// one-time bills are counted if their DueDate falls inside the month.
    /// </summary>
    public static decimal MonthlyObligationsForUser(IEnumerable<SplitWithBillDetail> splits, int year, int month)
    {
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEndExclusive = monthStart.AddMonths(1);

        decimal total = 0m;
        foreach (var s in splits)
        {
            int occurrences;
            if (s.RecurrenceFrequency.HasValue && s.RecurrenceStartDate.HasValue)
            {
                var schedule = RecurrenceSchedule.Create(
                    s.RecurrenceFrequency.Value,
                    s.RecurrenceStartDate.Value,
                    s.RecurrenceEndDate);
                occurrences = schedule.GetOccurrencesInRange(monthStart, monthEndExclusive).Count;
            }
            else
            {
                occurrences = (s.DueDate >= monthStart && s.DueDate < monthEndExclusive) ? 1 : 0;
            }
            total += occurrences * s.Amount;
        }
        return total;
    }
}
