namespace Finance.Domain.Engines;

/// <summary>
/// Pure auto-link policy for a recurring bank suggestion: the display name to give a created
/// income/charge, and the next due date for an outflow charge. Kept out of <c>BankSyncManager</c> so
/// the rules are testable without I/O; the manager still owns the "does a match already exist, then
/// create it" orchestration.
/// </summary>
public static class SuggestionLinkPolicy
{
    /// <summary>Prefer the merchant name, then the transaction description, else "Unknown".</summary>
    public static string ResolveSourceName(string? merchantName, string? description) =>
        !string.IsNullOrWhiteSpace(merchantName) ? merchantName
        : !string.IsNullOrWhiteSpace(description) ? description
        : "Unknown";

    /// <summary>
    /// The next due date for a created charge: the predicted next occurrence when known, otherwise
    /// the day after the last seen payment — never in the past (clamped to the day after
    /// <paramref name="today"/>).
    /// </summary>
    public static DateTime ResolveNextDue(DateTime? predictedNextDate, DateTime lastDate, DateTime today)
    {
        var nextDue = predictedNextDate ?? lastDate.AddDays(1);
        if (nextDue < today) nextDue = today.AddDays(1);
        return nextDue;
    }
}
