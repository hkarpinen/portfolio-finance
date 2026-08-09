using Finance.Domain.ValueObjects;

namespace Tests;

public class MoneyTests
{
    [Fact]
    public void Create_ShouldSetAmountAndCurrency()
    {
        var money = Money.Create(100.00m, "USD");

        Assert.Equal(100.00m, money.Amount);
        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void Create_ShouldNormalizeCurrencyToUppercase()
    {
        var money = Money.Create(50m, "usd");

        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void Create_NegativeAmount_IsAllowed_ForSignedLedgerAmounts()
    {
        // Money is signed (refunds, contra/reversing entries, bank inflows).
        // Non-negativity is a context invariant owned by the aggregates that need it.
        var money = Money.Create(-1m, "USD");
        Assert.Equal(-1m, money.Amount);
    }

    [Fact]
    public void Negate_ReturnsAdditiveInverse_SameCurrency()
    {
        var money = Money.Create(600m, "USD");
        var contra = money.Negate();
        Assert.Equal(-600m, contra.Amount);
        Assert.Equal("USD", contra.Currency);
    }

    [Fact]
    public void Create_EmptyCurrency_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => Money.Create(10m, ""));
    }

    [Fact]
    public void Create_InvalidCurrencyLength_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => Money.Create(10m, "US"));
    }

}
