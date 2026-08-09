// Wire contract for demo events consumed from RabbitMQ.
//
// The namespace and type names must match the publisher exactly — MassTransit binds both ends to
// `Infrastructure.Messaging.Events:DemoHouseholdSeededEvent`, and a mismatch binds a different
// exchange and misses every message silently.
namespace Infrastructure.Messaging.Events;

public sealed record DemoHouseholdSeededEvent(
    Guid Id,
    DateTime OccurredAt,
    Guid UserId,
    Guid HouseholdId);
