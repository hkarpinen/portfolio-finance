using Finance.Domain.Engines;

namespace Tests;

public class SuggestionLinkPolicyTests
{
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
}
