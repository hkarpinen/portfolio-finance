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
        RecurrenceFrequency.Weekly       => 52m / 12m,
        RecurrenceFrequency.BiWeekly     => 26m / 12m,
        RecurrenceFrequency.Annually     => 1m / 12m,
        RecurrenceFrequency.Quarterly    => 1m / 3m,
        RecurrenceFrequency.SemiAnnually => 1m / 6m,
        _                                => 1m,
    };
}
