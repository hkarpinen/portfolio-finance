namespace Finance.Domain.Engines;

/// <summary>
/// Pure allocation arithmetic, kept separate from <c>ChargeManager</c> so the rounding and
/// overshoot rules can be unit-tested without any repository/infrastructure plumbing.
/// </summary>
public static class AllocationMath
{
    /// <summary>
    /// Split <paramref name="total"/> evenly across <paramref name="memberCount"/> members, rounding
    /// each share to 2 decimals with the LAST member absorbing the rounding remainder so the shares
    /// sum EXACTLY to <paramref name="total"/> (e.g. 100 / 3 → 33.33, 33.33, 33.34).
    /// </summary>
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

    /// <summary>
    /// True when adding <paramref name="newAmount"/> on top of <paramref name="alreadyAllocated"/>
    /// (the sum of the charge's OTHER active allocations) stays within <paramref name="chargeTotal"/>.
    /// Equality is allowed — allocations may sum exactly to the charge total.
    /// </summary>
    public static bool Fits(decimal alreadyAllocated, decimal newAmount, decimal chargeTotal)
        => alreadyAllocated + newAmount <= chargeTotal;
}
