using System.Text.Json.Serialization;

namespace Finance.Domain.ValueObjects;

// A SIGNED monetary amount. The sign is meaningful: balances go negative, refunds and ledger
// contra/reversing entries are negative, and bank inflows are negative (the provider's
// convention). Non-negativity is NOT an intrinsic property of money — it is a context-specific
// invariant, enforced by the aggregates where it actually holds (Expense, Share and
// IncomeSource each guard amount < 0 in their own factories).
public readonly record struct Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    [JsonConstructor]
    private Money(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be empty.", nameof(currency));

        if (currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(currency));

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    public Money Negate() => new(-Amount, Currency);

    public static Money Create(decimal amount, string currency) => new(amount, currency);

    public override string ToString() => $"{Amount:F2} {Currency}";
}
