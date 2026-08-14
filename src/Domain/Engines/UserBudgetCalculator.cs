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
}
