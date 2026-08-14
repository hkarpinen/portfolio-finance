namespace Finance.Domain.Engines;

public static class ShareMath
{
    // The LAST member absorbs the rounding remainder so the shares sum EXACTLY to the total
    // (100 / 3 → 33.33, 33.33, 33.34).
    public static IReadOnlyList<decimal> SplitEvenly(decimal total, int memberCount)
    {
        if (memberCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(memberCount), "At least one member is required.");

        var per = Math.Round(total / memberCount, 2, MidpointRounding.ToEven);
        var shares = new decimal[memberCount];
        decimal running = 0m;
        for (var i = 0; i < memberCount - 1; i++)
        {
            shares[i] = per;
            running += per;
        }
        shares[memberCount - 1] = total - running; // last share absorbs the remainder
        return shares;
    }

    // alreadyAllocated is the sum of the expense's OTHER active shares. Equality is allowed —
    // shares may sum exactly to the expense total.
    public static bool Fits(decimal alreadyAllocated, decimal newAmount, decimal expenseTotal)
        => alreadyAllocated + newAmount <= expenseTotal;

    // The same rule read from the other end: shrinking a expense must not strand shares above
    // it. Journalizing such a expense has no account that can absorb the negative remainder, and
    // that failure surfaces inside the journalLine consumer where nobody can act on it.
    public static bool CoversShares(decimal expenseTotal, decimal allocatedTotal)
        => allocatedTotal <= expenseTotal;
}
