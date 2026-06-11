using System.Text.Json;
using System.Text.Json.Serialization;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Infrastructure.Persistence;

// Value-object JSON converters so domain events serialise to flat primitives
// rather than {"value":"..."} objects.

internal sealed class AllocationIdConverter : JsonConverter<AllocationId>
{
    public override AllocationId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(r.GetGuid());
    public override void Write(Utf8JsonWriter w, AllocationId v, JsonSerializerOptions o) => w.WriteStringValue(v.Value);
}

internal sealed class GroupIdConverter : JsonConverter<GroupId>
{
    public override GroupId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(r.GetGuid());
    public override void Write(Utf8JsonWriter w, GroupId v, JsonSerializerOptions o) => w.WriteStringValue(v.Value);
}

internal sealed class BillsUserIdConverter : JsonConverter<UserId>
{
    public override UserId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(r.GetGuid());
    public override void Write(Utf8JsonWriter w, UserId v, JsonSerializerOptions o) => w.WriteStringValue(v.Value);
}

internal sealed class ChargeIdConverter : JsonConverter<ChargeId>
{
    public override ChargeId Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(r.GetGuid());
    public override void Write(Utf8JsonWriter w, ChargeId v, JsonSerializerOptions o) => w.WriteStringValue(v.Value);
}


// Money has a private constructor and read-only properties, so STJ cannot
// round-trip it without a custom converter. We keep the nested wire shape
// (`{"amount": 100.00, "currency": "USD"}`) — consumers model it as a
// nested MoneyDto rather than splatting two flat properties.
internal sealed class MoneyConverter : JsonConverter<Money>
{
    public override Money Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected start of Money object.");

        decimal amount = 0m;
        string currency = "USD";

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return Money.Create(amount, currency);

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected property name.");

            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "amount":
                case "Amount":
                    amount = reader.GetDecimal();
                    break;
                case "currency":
                case "Currency":
                    currency = reader.GetString() ?? "USD";
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }
        throw new JsonException("Unexpected end of Money object.");
    }

    public override void Write(Utf8JsonWriter writer, Money value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("amount", value.Amount);
        writer.WriteString("currency", value.Currency);
        writer.WriteEndObject();
    }
}

// RecurrenceSchedule also hides its constructor behind a Create factory.
// The wire shape mirrors the property layout.
internal sealed class RecurrenceScheduleConverter : JsonConverter<RecurrenceSchedule>
{
    public override RecurrenceSchedule Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("Expected start of RecurrenceSchedule object.");

        RecurrenceFrequency frequency = default;
        DateTime startDate = default;
        DateTime? endDate = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return RecurrenceSchedule.Create(frequency, startDate, endDate);

            if (reader.TokenType != JsonTokenType.PropertyName)
                throw new JsonException("Expected property name.");

            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "frequency":
                case "Frequency":
                    // Enum value may arrive as a camelCase string thanks to
                    // JsonStringEnumConverter, or as an int — handle both.
                    if (reader.TokenType == JsonTokenType.String)
                        frequency = Enum.Parse<RecurrenceFrequency>(reader.GetString()!, ignoreCase: true);
                    else
                        frequency = (RecurrenceFrequency)reader.GetInt32();
                    break;
                case "startDate":
                case "StartDate":
                    startDate = reader.GetDateTime();
                    break;
                case "endDate":
                case "EndDate":
                    endDate = reader.TokenType == JsonTokenType.Null ? null : reader.GetDateTime();
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }
        throw new JsonException("Unexpected end of RecurrenceSchedule object.");
    }

    public override void Write(Utf8JsonWriter writer, RecurrenceSchedule value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        // Honour any registered enum converter (e.g. camelCase JsonStringEnumConverter).
        writer.WritePropertyName("frequency");
        JsonSerializer.Serialize(writer, value.Frequency, options);
        writer.WriteString("startDate", value.StartDate);
        if (value.EndDate.HasValue)
            writer.WriteString("endDate", value.EndDate.Value);
        else
            writer.WriteNull("endDate");
        writer.WriteEndObject();
    }
}

internal static class OutboxExtensions
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),
            new ChargeIdConverter(),
            new AllocationIdConverter(),
            new GroupIdConverter(),
            new BillsUserIdConverter(),
            new MoneyConverter(),
            new RecurrenceScheduleConverter()
        }
    };

    /// <summary>Ledger housekeeping events that never leave the service: they are raised by the
    /// ledger aggregates on every posting, carry nothing a consumer needs (the business facts ride
    /// the charge/allocation/settlement events), and would otherwise dead-letter as unknown types.
    /// The ledger-posting consumer reacts to charge-context events, so outboxing these would also
    /// echo every posting back onto the bus.</summary>
    private static readonly HashSet<Type> LedgerHousekeepingEvents =
    [
        typeof(LedgerOpened),
        typeof(AccountOpened),
        typeof(JournalEntryPosted),
        typeof(JournalEntryReversed),
    ];

    /// <summary>
    /// Serialises a domain event and appends it to the outbox_messages table.
    /// Call this for every domain event before SaveChangesAsync so both writes
    /// occur in the same transaction.
    /// </summary>
    public static void AddToOutbox(this FinanceDbContext context, DomainEvent domainEvent)
    {
        if (LedgerHousekeepingEvents.Contains(domainEvent.GetType())) return;

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
