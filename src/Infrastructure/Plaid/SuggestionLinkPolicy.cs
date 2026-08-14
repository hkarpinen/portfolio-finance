using Infrastructure.Plaid.Mirrors;
using Finance.Domain.ValueObjects;

namespace Infrastructure.Plaid;

/// <summary>What a suggestion the bank noticed should become, if anything.</summary>
public enum SuggestedDocument
{
    /// <summary>Already linked, so there is nothing to propose.</summary>
    None,
    IncomeSource,
    Expense,
}

/// <summary>
/// The proposal, decided without touching the database. Whether something matching already exists
/// is a separate question, and the only one the caller has to go and ask.
/// </summary>
public readonly record struct SuggestedLink(
    SuggestedDocument Document, string SourceName, Money Amount, DateTime NextDue);

public static class SuggestionLinkPolicy
{
    /// <summary>
    /// What this suggestion should be turned into. Inflow means money arriving, so it becomes an
    /// income source; outflow becomes an expense. The rule lived inside a private manager method
    /// with the database calls and the logging wrapped around it, which is what made it hard to see
    /// that it was a rule at all.
    /// </summary>
    public static SuggestedLink Propose(RecurringSuggestion suggestion, DateTime today)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        var sourceName = ResolveSourceName(suggestion.MerchantName, suggestion.Description);

        if (suggestion.IsLinked)
            return new SuggestedLink(SuggestedDocument.None, sourceName, suggestion.AverageAmount, today);

        return suggestion.Direction == RecurringFlowDirection.Inflow
            ? new SuggestedLink(
                SuggestedDocument.IncomeSource, sourceName, suggestion.AverageAmount, suggestion.LastDate)
            : new SuggestedLink(
                SuggestedDocument.Expense, sourceName, suggestion.AverageAmount,
                ResolveNextDue(suggestion.PredictedNextDate, suggestion.LastDate, today));
    }

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
