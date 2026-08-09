namespace Finance.Domain.Engines;

public static class SuggestionLinkPolicy
{
    public static string ResolveSourceName(string? merchantName, string? description) =>
        !string.IsNullOrWhiteSpace(merchantName) ? merchantName
        : !string.IsNullOrWhiteSpace(description) ? description
        : "Unknown";

    // The predicted next occurrence when known, otherwise the day after the last seen payment —
    // never in the past, always clamped to at least the day after today.
    public static DateTime ResolveNextDue(DateTime? predictedNextDate, DateTime lastDate, DateTime today)
    {
        var nextDue = predictedNextDate ?? lastDate.AddDays(1);
        if (nextDue < today) nextDue = today.AddDays(1);
        return nextDue;
    }
}
