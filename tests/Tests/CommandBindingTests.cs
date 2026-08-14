using System.Text.Json;
using Finance.Application.Commands;

namespace Tests;

/// <summary>
/// What the server already knows, the body may not say. Who is asking comes from the token and the
/// group comes from the route; a request naming either is not malformed — the binder would accept
/// it happily — so those fields are simply not readable from JSON.
/// </summary>
public class CommandBindingTests
{
    private static readonly JsonSerializerOptions Camel = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ABodyNamingTheCaller_IsNotHeard()
    {
        var impostor = Guid.NewGuid();
        var json = $$"""
            { "title": "Rent", "amount": 900, "currency": "USD", "callerUserId": "{{impostor}}" }
            """;

        var cmd = JsonSerializer.Deserialize<CreateExpenseCommand>(json, Camel)!;

        Assert.NotEqual(impostor, cmd.CallerUserId);
        Assert.Equal(Guid.Empty, cmd.CallerUserId);
    }

    [Fact]
    public void ABodyNamingTheGroup_IsNotHeard()
    {
        var elsewhere = Guid.NewGuid();
        var json = $$"""
            { "title": "Rent", "amount": 900, "currency": "USD", "groupId": "{{elsewhere}}" }
            """;

        var cmd = JsonSerializer.Deserialize<CreateGroupExpenseCommand>(json, Camel)!;

        Assert.NotEqual(elsewhere, cmd.GroupId);
    }

    // Unheard leaves Guid.Empty, which owns nothing and belongs to no group — so a controller that
    // forgets to fill it in is refused rather than trusted.
    [Fact]
    public void UnheardMeansEmpty_WhichPassesNoOwnerCheck()
    {
        var cmd = JsonSerializer.Deserialize<UpdateExpenseCommand>(
            """{ "title": "Rent", "amount": 900, "currency": "USD" }""", Camel)!;

        Assert.Equal(Guid.Empty, cmd.CallerUserId);
    }

    // Everything the caller legitimately supplies still binds.
    [Fact]
    public void TheRestOfTheBody_BindsAsBefore()
    {
        var cmd = JsonSerializer.Deserialize<CreateExpenseCommand>(
            """{ "title": "Rent", "amount": 900, "currency": "USD", "category": "Rent" }""", Camel)!;

        Assert.Equal("Rent", cmd.Title);
        Assert.Equal(900m, cmd.Amount);
        Assert.Equal("USD", cmd.Currency);
    }
}
