using Finance.Domain.Engines;

namespace Tests;

public class ShareMathTests
{
    [Fact]
    public void SplitEvenly_100By3_LastAbsorbsRemainder_SumsExactly()
    {
        var shares = ShareMath.SplitEvenly(100m, 3);

        Assert.Equal(new[] { 33.33m, 33.33m, 33.34m }, shares);
        Assert.Equal(100m, shares.Sum());
    }

    [Fact]
    public void SplitEvenly_EvenlyDivisible_AllSharesEqual()
    {
        var shares = ShareMath.SplitEvenly(100m, 4);

        Assert.Equal(new[] { 25m, 25m, 25m, 25m }, shares);
        Assert.Equal(100m, shares.Sum());
    }

    [Theory]
    [InlineData(100, 3)]
    [InlineData(100, 7)]
    [InlineData(0.10, 3)]
    [InlineData(99.99, 6)]
    [InlineData(50, 1)]
    public void SplitEvenly_AlwaysSumsToTotal(double total, int count)
    {
        var t = (decimal)total;
        var shares = ShareMath.SplitEvenly(t, count);

        Assert.Equal(count, shares.Count);
        Assert.Equal(t, shares.Sum());
    }

    [Fact]
    public void SplitEvenly_ZeroMembers_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => ShareMath.SplitEvenly(100m, 0));

    [Fact]
    public void Fits_ExactTotal_IsAllowed()
        => Assert.True(ShareMath.Fits(alreadyAllocated: 60m, newAmount: 40m, expenseTotal: 100m));

    [Fact]
    public void Fits_UnderTotal_IsAllowed()
        => Assert.True(ShareMath.Fits(alreadyAllocated: 60m, newAmount: 30m, expenseTotal: 100m));

    [Fact]
    public void Fits_Overshoot_IsRejected()
        => Assert.False(ShareMath.Fits(alreadyAllocated: 60m, newAmount: 50m, expenseTotal: 100m));

    [Fact]
    public void Fits_OvershootByCent_IsRejected()
        => Assert.False(ShareMath.Fits(alreadyAllocated: 99.99m, newAmount: 0.02m, expenseTotal: 100m));

    [Theory]
    [InlineData(100, 100, true)]   // split to the penny
    [InlineData(100, 60, true)]    // partly split
    [InlineData(60, 100, false)]   // shrunk below what is already split
    public void CoversShares_RefusesOnlyWhenTheExpenseIsTooSmall(
        decimal expenseTotal, decimal allocated, bool expected) =>
        Assert.Equal(expected, ShareMath.CoversShares(expenseTotal, allocated));
}
