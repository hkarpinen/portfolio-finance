using Finance.Domain.ReadModels;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Engines;

/// <summary>
/// Determines whether a synced bank outflow amount matches a linked recurring
/// suggestion well enough to auto-pay the associated expense.
/// Change driver: auto-pay tolerance rules and matching strategy.
/// </summary>
public interface IBankSyncMatchingEngine
{
    bool IsMatch(RecurringSuggestion suggestion, Guid accountId, decimal transactionAmount);
    RecurringFlowDirection ResolveDirection(decimal amount);
}

internal sealed class BankSyncMatchingEngine : IBankSyncMatchingEngine
{
    /// <summary>
    /// A transaction is considered a match when it is for the same account and
    /// its amount is within 5 % of the suggestion's average amount.
    /// </summary>
    private const decimal ToleranceRate = 0.05m;

    public bool IsMatch(RecurringSuggestion suggestion, Guid accountId, decimal transactionAmount)
    {
        if (suggestion.AccountId != accountId) return false;
        var deviation = Math.Abs(suggestion.AverageAmount.Amount - transactionAmount)
                        / Math.Max(transactionAmount, 0.01m);
        return deviation <= ToleranceRate;
    }

    public RecurringFlowDirection ResolveDirection(decimal amount)
        => amount > 0 ? RecurringFlowDirection.Outflow : RecurringFlowDirection.Inflow;
}
