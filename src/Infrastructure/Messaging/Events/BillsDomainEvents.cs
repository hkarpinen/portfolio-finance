namespace Infrastructure.Messaging.Events;

// Wire shapes for bills domain events published via the outbox to RabbitMQ.
// Must match the flat camelCase JSON produced by OutboxExtensions.AddToOutbox.

public sealed record BillsHouseholdCreatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid HouseholdId,
    string Name,
    Guid OwnerId,
    string CurrencyCode);

public sealed record BillsHouseholdOwnershipTransferredEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid HouseholdId,
    Guid NewOwnerId);

public sealed record BillsHouseholdMemberJoinedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid MembershipId,
    Guid HouseholdId,
    Guid UserId,
    string Role);

public sealed record BillsHouseholdMemberLeftEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid MembershipId,
    Guid HouseholdId,
    Guid UserId);

public sealed record BillsHouseholdMemberRemovedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid MembershipId,
    Guid HouseholdId,
    Guid UserId);

public sealed record BillsHouseholdMemberRoleChangedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid MembershipId,
    Guid HouseholdId,
    Guid UserId,
    string NewRole);

public sealed record BillsBillCreatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid BillId,
    Guid HouseholdId,
    string Title,
    Guid CreatedBy);

public sealed record BillsBillSplitCreatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid SplitId,
    Guid BillId,
    Guid HouseholdId,
    Guid MembershipId,
    Guid UserId);
