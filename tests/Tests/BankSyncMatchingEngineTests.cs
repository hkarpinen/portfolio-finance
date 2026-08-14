using Finance.Domain.Engines;
using Infrastructure.Plaid;
using Infrastructure.Plaid.Mirrors;
using Finance.Domain.ValueObjects;

namespace Tests;

public class BankSyncMatchingEngineTests
{
    private readonly IBankSyncMatchingEngine _engine = new BankSyncMatchingEngine();

    private static RecurringSuggestion CreateSuggestion(Guid accountId, decimal averageAmount) =>
        RecurringSuggestion.Create(
            new FinancialConnectionId(Guid.NewGuid()),
            accountId,
            new UserId(Guid.NewGuid()),
            "stream-001",
            RecurringFlowDirection.Outflow,
            "Netflix",
            null,
            RecurrenceFrequency.Monthly,
            Money.Create(averageAmount, "USD"),
            Money.Create(averageAmount, "USD"),
            DateTime.UtcNow.AddMonths(-3),
            DateTime.UtcNow.AddMonths(-1),
            null,
            true);

    [Fact]
    public void IsMatch_SameAccountExactAmount_ReturnsTrue()
    {
        var accountId = Guid.NewGuid();
        var suggestion = CreateSuggestion(accountId, 100m);

        Assert.True(_engine.IsMatch(suggestion, accountId, 100m));
    }

    [Fact]
    public void IsMatch_SameAccountWithinFivePercentTolerance_ReturnsTrue()
    {
        var accountId = Guid.NewGuid();
        var suggestion = CreateSuggestion(accountId, 100m);

        // 4.99% deviation — within tolerance
        Assert.True(_engine.IsMatch(suggestion, accountId, 104.99m));
    }

    [Fact]
    public void IsMatch_SameAccountExceedsTolerance_ReturnsFalse()
    {
        var accountId = Guid.NewGuid();
        var suggestion = CreateSuggestion(accountId, 100m);

        // 10% deviation — exceeds 5% tolerance
        Assert.False(_engine.IsMatch(suggestion, accountId, 110m));
    }

    [Fact]
    public void IsMatch_DifferentAccount_ReturnsFalse()
    {
        var accountId = Guid.NewGuid();
        var differentAccountId = Guid.NewGuid();
        var suggestion = CreateSuggestion(accountId, 100m);

        Assert.False(_engine.IsMatch(suggestion, differentAccountId, 100m));
    }

    [Fact]
    public void IsMatch_DifferentAccountAndAmountOutOfTolerance_ReturnsFalse()
    {
        var suggestion = CreateSuggestion(Guid.NewGuid(), 100m);

        Assert.False(_engine.IsMatch(suggestion, Guid.NewGuid(), 200m));
    }

    [Theory]
    [InlineData(1, RecurringFlowDirection.Outflow)]
    [InlineData(100, RecurringFlowDirection.Outflow)]
    [InlineData(-1, RecurringFlowDirection.Inflow)]
    [InlineData(-100, RecurringFlowDirection.Inflow)]
    public void ResolveDirection_PositiveAmountIsOutflow_NegativeAmountIsInflow(decimal amount, RecurringFlowDirection expected)
    {
        Assert.Equal(expected, _engine.ResolveDirection(amount));
    }
}
