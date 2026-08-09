using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Finance.Domain.Events;
using Infrastructure.Messaging;
using Finance.Domain.ValueObjects;

namespace Infrastructure.Persistence;

// Every strongly-typed id in this service wraps a single Guid, so one factory covers them all and
// a NEW id type serialises flat from the day it is introduced. Hand-written per-type converters
// silently emit {"value":"…"} for anything nobody remembered to add.
internal sealed class StronglyTypedIdConverter : JsonConverterFactory
{
    public override bool CanConvert(Type type) =>
        type.GetProperty("Value")?.PropertyType == typeof(Guid)
        && type.GetConstructor([typeof(Guid)]) is not null;

    public override JsonConverter CreateConverter(Type type, JsonSerializerOptions _) =>
        (JsonConverter)Activator.CreateInstance(typeof(Inner<>).MakeGenericType(type))!;

    private sealed class Inner<T> : JsonConverter<T>
    {
        private static readonly Func<Guid, T> Wrap = BuildWrap();
        private static readonly Func<T, Guid> Unwrap = BuildUnwrap();

        private static Func<Guid, T> BuildWrap()
        {
            var g = Expression.Parameter(typeof(Guid));
            return Expression.Lambda<Func<Guid, T>>(
                Expression.New(typeof(T).GetConstructor([typeof(Guid)])!, g), g).Compile();
        }

        private static Func<T, Guid> BuildUnwrap()
        {
            var id = Expression.Parameter(typeof(T));
            return Expression.Lambda<Func<T, Guid>>(Expression.Property(id, "Value"), id).Compile();
        }

        public override T Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => Wrap(r.GetGuid());
        public override void Write(Utf8JsonWriter w, T v, JsonSerializerOptions o) => w.WriteStringValue(Unwrap(v));
    }
}


// Money has a private constructor and read-only properties, so STJ cannot round-trip it without a
// custom converter. The nested wire shape (`{"amount": 100.00, "currency": "USD"}`) is deliberate
// — it is not splatted into two flat properties.
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
                    // The enum may arrive as a camelCase string (JsonStringEnumConverter) or as an int — handle both.
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
            new StronglyTypedIdConverter(),
            new MoneyConverter(),
            new RecurrenceScheduleConverter()
        }
    };

    // Call this for every domain event BEFORE SaveChangesAsync, so the event row and the aggregate
    // write land in the same transaction.
    public static void AddToOutbox(this FinanceDbContext context, DomainEvent domainEvent)
    {
        // Not publishable, not outboxed. Ledger housekeeping lands here: those events fire on every
        // posting, carry nothing a consumer needs, and outboxing them would echo every posting back
        // onto the bus for the ledger-posting consumer to react to.
        if (!PublishedEvents.Includes(domainEvent.GetType())) return;

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
