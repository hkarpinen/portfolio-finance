namespace Finance.Domain.ValueObjects;

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

    /// <summary>
    /// From an optional frequency — null, blank or unrecognised means "does not repeat", which is
    /// a real answer rather than an error: a one-off expense is the common case.
    /// </summary>
    public static RecurrenceSchedule? CreateOrNone(RecurrenceFrequency? frequency, DateTime? startDate, DateTime? endDate = null)
        => frequency is null ? null : Create(frequency.Value, startDate ?? DateTime.UtcNow, endDate);

    /// <summary>The same, from the wire, where a frequency arrives as text.</summary>
    public static RecurrenceSchedule? ParseOrNone(string? frequency, DateTime? startDate, DateTime? endDate = null)
        => Enum.TryParse<RecurrenceFrequency>(frequency, ignoreCase: true, out var parsed)
            ? CreateOrNone(parsed, startDate, endDate)
            : null;

    public static RecurrenceSchedule Create(RecurrenceFrequency frequency, DateTime startDate, DateTime? endDate = null)
        => new(
            frequency,
            DateTime.SpecifyKind(startDate, DateTimeKind.Utc),
            endDate.HasValue ? DateTime.SpecifyKind(endDate.Value, DateTimeKind.Utc) : null);

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

    public decimal GetAmountForPeriod(Money amount, DateTime periodStart, DateTime periodEnd)
    {
        var occurrences = GetOccurrencesInRange(periodStart, periodEnd);
        return occurrences.Count * amount.Amount;
    }

    // The most recent occurrence on or before today — or the FIRST upcoming one if the schedule
    // has not started yet. Falls back to the given date when the schedule yields no occurrences.
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
