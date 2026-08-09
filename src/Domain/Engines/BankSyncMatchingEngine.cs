using Finance.Domain.ReadModels;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Engines;

public interface IBankSyncMatchingEngine
{
    bool IsMatch(RecurringSuggestion suggestion, Guid accountId, decimal transactionAmount);
    RecurringFlowDirection ResolveDirection(decimal amount);
}

internal sealed class BankSyncMatchingEngine : IBankSyncMatchingEngine
{
    // A match is the same account AND an amount within this fraction of the suggestion's average.
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
