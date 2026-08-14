using Finance.Domain.Engines;
using Finance.Domain.ReadModels;
using Finance.Domain.ValueObjects;

namespace Tests;

public class SuggestionLinkPolicyTests
{
    private static readonly DateTime Today = new(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private static RecurringSuggestion Suggestion(RecurringFlowDirection direction) =>
        RecurringSuggestion.Create(
            FinancialConnectionId.New(), Guid.NewGuid(), UserId.New(), "stream-1", direction,
            "Acme subscription", "Acme Co", RecurrenceFrequency.Monthly,
            Money.Create(12m, "USD"), Money.Create(12m, "USD"),
            Today.AddMonths(-3), Today.AddDays(-2), Today.AddDays(28), isActive: true);

    [Theory]
    [InlineData("Acme Co", "card purchase", "Acme Co")]   // merchant wins
    [InlineData("", "card purchase", "card purchase")]      // fall back to description
    [InlineData(null, null, "Unknown")]                     // nothing → Unknown
    [InlineData("  ", "  ", "Unknown")]                     // whitespace → Unknown
    public void ResolveSourceName_PrefersMerchant_ThenDescription_ElseUnknown(
        string? merchant, string? description, string expected) =>
        Assert.Equal(expected, SuggestionLinkPolicy.ResolveSourceName(merchant, description));

    [Fact]
    public void ResolveNextDue_UsesPredicted_WhenInFuture()
    {
        var today = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var predicted = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(predicted, SuggestionLinkPolicy.ResolveNextDue(predicted, lastDate: today.AddDays(-30), today));
    }

    [Fact]
    public void ResolveNextDue_FallsBackToDayAfterLast_WhenNoPrediction()
    {
        var today = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var last = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(last.AddDays(1), SuggestionLinkPolicy.ResolveNextDue(null, last, today));
    }

    [Fact]
    public void ResolveNextDue_ClampsPastDatesToTomorrow()
    {
        var today = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var pastLast = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc); // last+1 is still in the past
        Assert.Equal(today.AddDays(1), SuggestionLinkPolicy.ResolveNextDue(null, pastLast, today));
    }

    // The decision used to sit inside a private manager method, wrapped in database calls and
    // logging, which is what made it hard to see that it was a decision at all.
    [Fact]
    public void MoneyArriving_BecomesAnIncomeSource()
    {
        var proposal = SuggestionLinkPolicy.Propose(Suggestion(RecurringFlowDirection.Inflow), Today);

        Assert.Equal(SuggestedDocument.IncomeSource, proposal.Document);
        Assert.Equal("Acme Co", proposal.SourceName);
    }

    [Fact]
    public void MoneyLeaving_BecomesAnExpense()
    {
        var proposal = SuggestionLinkPolicy.Propose(Suggestion(RecurringFlowDirection.Outflow), Today);

        Assert.Equal(SuggestedDocument.Expense, proposal.Document);
        // An outflow is dated when it next falls due; an inflow when it last arrived.
        Assert.Equal(Today.AddDays(28), proposal.NextDue);
    }

    // Already linked means somebody acted on it. Proposing again would hand them a second copy of
    // a bill they already keep.
    [Fact]
    public void AnAlreadyLinkedSuggestion_ProposesNothing()
    {
        var suggestion = Suggestion(RecurringFlowDirection.Outflow);
        suggestion.MarkLinked(Guid.NewGuid(), LinkedEntityType.Expense);

        Assert.Equal(SuggestedDocument.None, SuggestionLinkPolicy.Propose(suggestion, Today).Document);
    }
}
