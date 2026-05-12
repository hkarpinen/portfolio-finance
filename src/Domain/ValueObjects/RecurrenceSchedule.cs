namespace Finance.Domain.ValueObjects;

/// <summary>
/// Recurrence schedule value object for recurring bills and income sources.
/// </summary>
public record RecurrenceSchedule
{
    public RecurrenceFrequency Frequency { get; }
    public DateTime StartDate { get; }
    public DateTime? EndDate { get; }

    private RecurrenceSchedule() { }

    private RecurrenceSchedule(RecurrenceFrequency frequency, DateTime startDate, DateTime? endDate)
    {
        if (endDate.HasValue && endDate.Value <= startDate)
            throw new ArgumentException("End date must be after start date.", nameof(endDate));

        Frequency = frequency;
        StartDate = startDate;
        EndDate = endDate;
    }

    public static RecurrenceSchedule Create(RecurrenceFrequency frequency, DateTime startDate, DateTime? endDate = null)
        => new(
            frequency,
            DateTime.SpecifyKind(startDate, DateTimeKind.Utc),
            endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : null);

    /// <summary>
    /// Gets the occurrences of the recurrence within the specified date range.
    /// </summary>
    public List<DateTime> GetOccurrencesInRange(DateTime rangeStart, DateTime rangeEnd)
    {
        if (rangeStart >= rangeEnd)
            throw new ArgumentException("Range start must be before range end.");

        var occurrences = new List<DateTime>();

        if (StartDate >= rangeEnd || (EndDate.HasValue && EndDate.Value <= rangeStart))
            return occurrences;

        var current = StartDate;
        var effectiveEnd = EndDate.HasValue
            ? (EndDate.Value < rangeEnd ? EndDate.Value : rangeEnd)
            : rangeEnd;

        while (current < effectiveEnd)
        {
            if (current >= rangeStart && current < rangeEnd)
                occurrences.Add(current);

            current = GetNextOccurrence(current);

            if (current >= effectiveEnd)
                break;
        }

        return occurrences;
    }

    /// <summary>
    /// Gets the total amount for a given period based on the recurrence frequency.
    /// </summary>
    public decimal GetAmountForPeriod(Money amount, DateTime periodStart, DateTime periodEnd)
    {
        var occurrences = GetOccurrencesInRange(periodStart, periodEnd);
        return occurrences.Count * amount.Amount;
    }

    /// <summary>
    /// Returns the most recent occurrence on or before today, or the first upcoming
    /// occurrence if the schedule hasn't started yet. Falls back to
    /// <paramref name="fallback"/> when no occurrences can be found.
    /// </summary>
    public DateTime CurrentOccurrence(DateTime fallback)
    {
        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var past = GetOccurrencesInRange(today.AddYears(-3), today.AddDays(1));
        if (past.Count > 0)
            return past[^1];

        var future = GetOccurrencesInRange(today, today.AddYears(2).AddDays(1));
        return future.Count > 0 ? future[0] : fallback;
    }

    private DateTime GetNextOccurrence(DateTime current) => Frequency switch
    {
        RecurrenceFrequency.Daily => current.AddDays(1),
        RecurrenceFrequency.Weekly => current.AddDays(7),
        RecurrenceFrequency.BiWeekly => current.AddDays(14),
        RecurrenceFrequency.Monthly => current.AddMonths(1),
        RecurrenceFrequency.Quarterly => current.AddMonths(3),
        RecurrenceFrequency.SemiAnnually => current.AddMonths(6),
        RecurrenceFrequency.Annually => current.AddYears(1),
        _ => throw new InvalidOperationException($"Unknown frequency: {Frequency}")
    };

    public override string ToString() => $"{Frequency} from {StartDate:yyyy-MM-dd}" + 
        (EndDate.HasValue ? $" to {EndDate.Value:yyyy-MM-dd}" : " (ongoing)");
}
