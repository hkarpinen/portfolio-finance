namespace Finance.Domain.ValueObjects;

/// <summary>
/// Money value object — a <em>signed</em> monetary amount in a currency (the classic
/// Money pattern). Sign is meaningful: balances go negative, refunds and ledger
/// contra/reversing entries are negative, and bank inflows are negative (Plaid
/// convention — see <c>FinancialTransaction.IsInflow</c>). Non-negativity is NOT an
/// intrinsic property of money; it is a context-specific invariant enforced by the
/// aggregates where it actually holds (<c>Charge</c>, <c>Allocation</c> and
/// <c>IncomeSource</c> each guard <c>amount &lt; 0</c> in their own factories).
/// </summary>
public readonly record struct Money
{
    public decimal Amount { get; }
    public string Currency { get; }

    private Money(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be empty.", nameof(currency));

        if (currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(currency));

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    /// <summary>The additive inverse — the contra amount for a reversing ledger entry.</summary>
    public Money Negate() => new(-Amount, Currency);

    public static Money Create(decimal amount, string currency) => new(amount, currency);

    public override string ToString() => $"{Amount:F2} {Currency}";
}
