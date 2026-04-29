using System.Text.Json;
using System.Text.Json.Serialization;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Infrastructure.Persistence;

// Value-object JSON converters so bills domain events serialise to flat primitives
// rather than {"value":"..."} objects.

internal sealed class BillIdConverter : JsonConverter<BillId>
{
    public override BillId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(r.GetGuid());
    public override void Write(Utf8JsonWriter w, BillId v, JsonSerializerOptions o) => w.WriteStringValue(v.Value);
}

internal sealed class HouseholdIdConverter : JsonConverter<HouseholdId>
{
    public override HouseholdId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(r.GetGuid());
    public override void Write(Utf8JsonWriter w, HouseholdId v, JsonSerializerOptions o) => w.WriteStringValue(v.Value);
}

internal sealed class BillsMembershipIdConverter : JsonConverter<MembershipId>
{
    public override MembershipId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(r.GetGuid());
    public override void Write(Utf8JsonWriter w, MembershipId v, JsonSerializerOptions o) => w.WriteStringValue(v.Value);
}

internal sealed class BillsUserIdConverter : JsonConverter<UserId>
{
    public override UserId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(r.GetGuid());
    public override void Write(Utf8JsonWriter w, UserId v, JsonSerializerOptions o) => w.WriteStringValue(v.Value);
}

internal sealed class SplitIdConverter : JsonConverter<SplitId>
{
    public override SplitId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(r.GetGuid());
    public override void Write(Utf8JsonWriter w, SplitId v, JsonSerializerOptions o) => w.WriteStringValue(v.Value);
}

internal static class OutboxExtensions
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new BillIdConverter(),
            new HouseholdIdConverter(),
            new BillsMembershipIdConverter(),
            new BillsUserIdConverter(),
            new SplitIdConverter()
        }
    };

    /// <summary>
    /// Serialises a domain event and appends it to the outbox_messages table.
    /// Call this for every domain event before SaveChangesAsync so both writes
    /// occur in the same transaction.
    /// </summary>
    public static void AddToOutbox(this FinanceDbContext context, DomainEvent domainEvent)
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = domainEvent.GetType().Name,
            Payload = JsonSerializer.Serialize<object>(domainEvent, JsonOptions),
            CreatedAt = DateTime.UtcNow,
            Published = false
        };

        context.OutboxMessages.Add(message);
    }
}
