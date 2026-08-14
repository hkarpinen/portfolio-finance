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

    // Whether a set of shares fits inside an expense is the EXPENSE's question — see
    // Expense.CanBear and Expense.WouldStillCover. It lived here taking the total as an argument,
    // which let a caller hand it one that was not the expense's.
}
