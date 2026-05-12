namespace Finance.Domain.ValueObjects;

/// <summary>
/// Recurrence frequency enumeration.
/// </summary>
public enum RecurrenceFrequency
{
    Daily = 0,
    Weekly = 1,
    BiWeekly = 2,
    Monthly = 3,
    Quarterly = 4,
    SemiAnnually = 5,
    Annually = 6
}

public static class RecurrenceFrequencyExtensions
{
    /// <summary>
    /// Returns the multiplier to convert one period's amount into a monthly equivalent.
    /// e.g. Weekly → amount × (52/12); Annually → amount / 12.
    /// </summary>
    public static decimal ToMonthlyFactor(this RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Daily        => 365m / 12m,
        RecurrenceFrequency.Weekly       => 52m / 12m,
        RecurrenceFrequency.BiWeekly     => 26m / 12m,
        RecurrenceFrequency.Monthly      => 1m,
        RecurrenceFrequency.Quarterly    => 1m / 3m,
        RecurrenceFrequency.SemiAnnually => 1m / 6m,
        RecurrenceFrequency.Annually     => 1m / 12m,
        _                                => throw new InvalidOperationException($"Unknown frequency: {frequency}"),
    };

    /// <summary>
    /// How many times this frequency occurs in a standard year.
    /// Used to convert between an "amount per [period]" and the per-paycheck amount.
    /// </summary>
    public static decimal PeriodsPerYear(this RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Daily        => 365m,
        RecurrenceFrequency.Weekly       => 52m,
        RecurrenceFrequency.BiWeekly     => 26m,
        RecurrenceFrequency.Monthly      => 12m,
        RecurrenceFrequency.Quarterly    => 4m,
        RecurrenceFrequency.SemiAnnually => 2m,
        RecurrenceFrequency.Annually     => 1m,
        _                                => 12m,
    };
}
