using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.Json;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Tests;

// The outbox wire contract: every strongly-typed id must serialise as a FLAT guid string. A
// consumer declares `Guid ExpenseId`, so a `{"value":"…"}` envelope binds to nothing and the field
// arrives empty — silently, with no deserialisation error.
public class OutboxSerializationTests
{
    private static readonly JsonSerializerOptions Options = (JsonSerializerOptions)
        typeof(Infrastructure.Persistence.FinanceDbContext).Assembly
            .GetType("Infrastructure.Persistence.OutboxExtensions")!
            .GetField("JsonOptions", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

    private static string Serialize<T>(T ev) => JsonSerializer.Serialize(ev, Options);

    [Fact]
    public void EveryIdOnAExpenseEventIsAFlatGuid()
    {
        var json = Serialize(new ExpenseCreated(
            ExpenseId.New(), UserId.New(), "Rent", Money.Create(100m, "USD"),
            ExpenseCategory.Rent, DateTime.UtcNow,
            GroupId.Create(Guid.NewGuid()), Guid.NewGuid()));

        Assert.DoesNotContain("\"value\"", json);
    }

    // These ids had no hand-written converter and serialised as `{"value":"…"}`. The factory covers
    // every id type, so a new one is flat from the day it is introduced.
    [Fact]
    public void IdsWithoutAHandWrittenConverterAreAlsoFlat()
    {
        var income = Serialize(new IncomeSourceCreated(
            IncomeId.New(), UserId.New(), Money.Create(50m, "USD"), "Job",
            RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, DateTime.UtcNow.Date)));
        var connection = Serialize(new FinancialConnectionEstablished(
            FinancialConnectionId.New(), UserId.New(), "Chase"));

        Assert.DoesNotContain("\"value\"", income);
        Assert.DoesNotContain("\"value\"", connection);
    }

    // Money is the deliberate exception: it stays a nested {amount, currency} object, and consumers
    // model it the same way. Flattening it would be a breaking wire change.
    [Fact]
    public void MoneyStaysNested()
    {
        var json = Serialize(new IncomeSourceCreated(
            IncomeId.New(), UserId.New(), Money.Create(50m, "USD"), "Job",
            RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, DateTime.UtcNow.Date)));

        Assert.Contains("\"amount\":50", json);
        Assert.Contains("\"currency\":\"USD\"", json);
    }

    [Fact]
    public void EventsRoundTripThroughTheOutboxOptions()
    {
        var original = new ExpenseCreated(
            ExpenseId.New(), UserId.New(), "Rent", Money.Create(100m, "USD"),
            ExpenseCategory.Rent, DateTime.UtcNow,
            GroupId.Create(Guid.NewGuid()), Guid.NewGuid());

        var back = JsonSerializer.Deserialize<ExpenseCreated>(Serialize(original), Options);

        Assert.Equal(original, back);
    }

    // The factory claims types structurally (a Guid `Value` plus a Guid constructor) rather than by
    // name, so the guard that matters is what it must NOT claim: Money and RecurrenceSchedule have
    // their own wire shapes, and flattening either is a breaking change.
    [Fact]
    public void TheFactoryClaimsIdsAndNothingElse()
    {
        var factory = (JsonConverterFactory)Activator.CreateInstance(
            typeof(Infrastructure.Persistence.FinanceDbContext).Assembly
                .GetType("Infrastructure.Persistence.StronglyTypedIdConverter")!)!;

        Assert.True(factory.CanConvert(typeof(ExpenseId)));
        Assert.True(factory.CanConvert(typeof(IncomeId)));
        Assert.False(factory.CanConvert(typeof(Money)));
        Assert.False(factory.CanConvert(typeof(RecurrenceSchedule)));
        Assert.False(factory.CanConvert(typeof(JournalLineDraft)));
        Assert.False(factory.CanConvert(typeof(Guid)));
    }

    // A personal expense carries no GroupId. The nullable strongly-typed id must survive the round
    // trip as null rather than becoming an empty Guid.
    [Fact]
    public void ANullableIdRoundTripsAsNull()
    {
        var personal = new ExpenseCreated(
            ExpenseId.New(), UserId.New(), "Rent", Money.Create(100m, "USD"),
            ExpenseCategory.Rent, DateTime.UtcNow, null, null);

        var back = JsonSerializer.Deserialize<ExpenseCreated>(Serialize(personal), Options);

        Assert.Null(back!.GroupId);
        Assert.Equal(personal, back);
    }

    // The gap this closes: an event that is drained to the outbox but has no wire type resolves to
    // nothing on the way out and dead-letters on its first attempt — silently, because the write
    // side never sees the failure. Gating the outbox on the same list makes the two impossible to
    // disagree, so this holds for EVERY domain event, including ones added later.
    [Fact]
    public void NoDomainEventCanReachTheOutboxWithoutAWireType()
    {
        var infra = typeof(Infrastructure.Persistence.FinanceDbContext).Assembly;
        var published = infra.GetType("Infrastructure.Messaging.PublishedEvents")!;
        var includes = published.GetMethod("Includes", BindingFlags.NonPublic | BindingFlags.Static)!;
        var tryResolve = published.GetMethod("TryResolve", BindingFlags.NonPublic | BindingFlags.Static)!;

        var domainEvents = typeof(ExpenseCreated).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(DomainEvent)) && !t.IsAbstract);

        foreach (var ev in domainEvents)
        {
            if (!(bool)includes.Invoke(null, [ev])!) continue;   // never enters the outbox

            var args = new object?[] { ev.Name, null };
            Assert.True((bool)tryResolve.Invoke(null, args)!, $"{ev.Name} is outboxed but has no wire type");
        }
    }

    /// <summary>
    /// Money and RecurrenceSchedule bind through their own constructors ([JsonConstructor] on the
    /// private one) rather than a hand-written converter. That deleted 115 lines and closed a hole:
    /// the old MoneyConverter defaulted a missing currency to "USD", so a truncated payload
    /// deserialised into a plausible-looking amount in the wrong currency.
    /// </summary>
    [Fact]
    public void MoneyWithNoCurrencyIsRefused_NotQuietlyMadeDollars()
    {
        Assert.ThrowsAny<Exception>(() =>
            JsonSerializer.Deserialize<Money>("""{"amount":50}""", Options));
    }
}
