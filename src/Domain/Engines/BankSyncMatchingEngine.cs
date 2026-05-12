using Finance.Domain.Aggregates;

namespace Finance.Domain.Engines;

/// <summary>
/// Determines whether a synced bank outflow amount matches a linked recurring
/// suggestion well enough to auto-pay the associated expense.
/// Change driver: auto-pay tolerance rules and matching strategy.
/// </summary>
public static class BankSyncMatchingEngine
{
    /// <summary>
    /// A transaction is considered a match when it is for the same account and
    /// its amount is within 5 % of the suggestion's average amount.
    /// </summary>
    private const decimal ToleranceRate = 0.05m;

    public static bool IsMatch(RecurringSuggestion suggestion, Guid accountId, decimal transactionAmount)
    {
        if (suggestion.AccountId != accountId) return false;
        var deviation = Math.Abs(suggestion.AverageAmount.Amount - transactionAmount)
                        / Math.Max(transactionAmount, 0.01m);
        return deviation <= ToleranceRate;
    }

    public static string ResolveDirection(decimal amount) => amount > 0 ? "expense" : "income";
}
