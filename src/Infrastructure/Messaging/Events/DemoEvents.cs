// Wire contract for household-service demo events consumed from RabbitMQ.
// The namespace + type name must match what household publishes — its file
// `HouseholdDemoEvents.cs` lives in `Infrastructure.Messaging.Events` with
// the same `DemoHouseholdSeededEvent` name, so MassTransit binds both ends
// to the same `Infrastructure.Messaging.Events:DemoHouseholdSeededEvent`
// exchange. Identity-published demo events live in `IdentityEvents.cs`.
namespace Infrastructure.Messaging.Events;

public sealed record DemoHouseholdSeededEvent(
    Guid Id,
    DateTime OccurredAt,
    Guid UserId,
    Guid HouseholdId);
