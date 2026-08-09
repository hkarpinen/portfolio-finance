using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Engines;

public static class UserBudgetCalculator
{
    public static decimal MonthlyEquivalent(decimal amount, RecurrenceFrequency frequency) =>
        amount * frequency.ToMonthlyFactor();

    public static decimal AnnualAmount(decimal amount, RecurrenceFrequency amountFrequency) =>
        amount * amountFrequency.PeriodsPerYear();

    // amountFrequency is the period the entered amount covers, paymentFrequency is how often a
    // paycheck lands: $80,000 Annually paid BiWeekly → 80,000 / 26 = $3,076.92 per paycheck.
    public static decimal PerPaycheckAmount(decimal amount, RecurrenceFrequency amountFrequency, RecurrenceFrequency paymentFrequency) =>
        AnnualAmount(amount, amountFrequency) / paymentFrequency.PeriodsPerYear();

    public static decimal MonthlyObligationsForUser(
        IEnumerable<(Allocation Allocation, Charge Charge)> splits,
        int year, int month)
    {
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEndExclusive = monthStart.AddMonths(1);

        decimal total = 0m;
        foreach (var s in splits)
        {
            int occurrences = s.Charge.RecurrenceSchedule is not null
                ? s.Charge.RecurrenceSchedule.GetOccurrencesInRange(monthStart, monthEndExclusive).Count
                : (s.Charge.DueDate >= monthStart && s.Charge.DueDate < monthEndExclusive) ? 1 : 0;
            total += occurrences * s.Allocation.Amount.Amount;
        }
        return total;
    }
}
