using Finance.Domain.ReadModels;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Utilities;

/// <summary>
/// Shared helpers for computing a user's personal budget figures (income and obligations).
/// Only pure Domain types are used here. Callers in the Application layer are responsible
/// for unpacking Application DTOs into primitive arguments before calling these methods.
/// </summary>
public static class UserBudgetCalculator
{
    /// <summary>
    /// Returns the monthly-equivalent of a raw amount + frequency combination.
    /// </summary>
    public static decimal MonthlyEquivalent(decimal amount, RecurrenceFrequency frequency) =>
        amount * frequency.ToMonthlyFactor();

    /// <summary>
    /// Converts an amount quoted in <paramref name="amountFrequency"/> to its annual equivalent.
    /// e.g. $6,000/month → $72,000/year; $80,000/year → $80,000/year.
    /// </summary>
    public static decimal AnnualAmount(decimal amount, RecurrenceFrequency amountFrequency) =>
        amount * amountFrequency.PeriodsPerYear();

    /// <summary>
    /// Returns the per-paycheck (per payment-cadence period) amount given:
    /// <list type="bullet">
    ///   <item><paramref name="amount"/> — the raw amount as entered by the user</item>
    ///   <item><paramref name="amountFrequency"/> — the period that amount represents (e.g. Annually for a salary)</item>
    ///   <item><paramref name="paymentFrequency"/> — how often a paycheck actually arrives (e.g. BiWeekly)</item>
    /// </list>
    /// Example: $80,000 Annually paid BiWeekly → $80,000 / 26 = $3,076.92 per paycheck.
    /// </summary>
    public static decimal PerPaycheckAmount(decimal amount, RecurrenceFrequency amountFrequency, RecurrenceFrequency paymentFrequency) =>
        AnnualAmount(amount, amountFrequency) / paymentFrequency.PeriodsPerYear();

    /// <summary>
    /// Recurring bills are projected into the month via <see cref="RecurrenceSchedule.GetOccurrencesInRange"/>;
    /// one-time bills are counted if their DueDate falls inside the month.
    /// </summary>
    public static decimal MonthlyObligationsForUser(IEnumerable<SplitWithSharedExpenseDetail> splits, int year, int month)
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
