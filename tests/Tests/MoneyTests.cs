using Bills.Domain.ValueObjects;

namespace Tests;

public class MoneyTests
{
    [Fact]
    public void Create_ShouldSetAmountAndCurrency()
    {
        // Arrange / Act
        var money = Money.Create(100.00m, "USD");

        // Assert
        Assert.Equal(100.00m, money.Amount);
        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void Create_ShouldNormalizeCurrencyToUppercase()
    {
        // Arrange / Act
        var money = Money.Create(50m, "usd");

        // Assert
        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void Create_NegativeAmount_ShouldThrow()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => Money.Create(-1m, "USD"));
    }

    [Fact]
    public void Create_EmptyCurrency_ShouldThrow()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => Money.Create(10m, ""));
    }

    [Fact]
    public void Create_InvalidCurrencyLength_ShouldThrow()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => Money.Create(10m, "US"));
    }

    [Fact]
    public void Add_SameCurrency_ShouldReturnSum()
    {
        // Arrange
        var a = Money.Create(100m, "USD");
        var b = Money.Create(50m, "USD");

        // Act
        var result = a.Add(b);

        // Assert
        Assert.Equal(150m, result.Amount);
        Assert.Equal("USD", result.Currency);
    }

    [Fact]
    public void Add_DifferentCurrencies_ShouldThrow()
    {
        // Arrange
        var usd = Money.Create(100m, "USD");
        var eur = Money.Create(100m, "EUR");

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => usd.Add(eur));
    }

    [Fact]
    public void Subtract_SameCurrency_ShouldReturnDifference()
    {
        // Arrange
        var a = Money.Create(100m, "USD");
        var b = Money.Create(30m, "USD");

        // Act
        var result = a.Subtract(b);

        // Assert
        Assert.Equal(70m, result.Amount);
    }

    [Fact]
    public void Subtract_DifferentCurrencies_ShouldThrow()
    {
        // Arrange
        var usd = Money.Create(100m, "USD");
        var eur = Money.Create(50m, "EUR");

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => usd.Subtract(eur));
    }

    [Fact]
    public void Multiply_ByFactor_ShouldReturnScaledAmount()
    {
        // Arrange
        var money = Money.Create(100m, "USD");

        // Act
        var result = money.Multiply(1.5m);

        // Assert
        Assert.Equal(150m, result.Amount);
    }

    [Fact]
    public void Multiply_ByNegativeFactor_ShouldThrow()
    {
        // Arrange
        var money = Money.Create(100m, "USD");

        // Act / Assert
        Assert.Throws<ArgumentException>(() => money.Multiply(-1m));
    }
}
