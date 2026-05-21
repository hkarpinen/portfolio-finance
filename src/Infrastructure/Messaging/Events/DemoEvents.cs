namespace Infrastructure.Messaging.Events;

public sealed record DemoUserCreatedEvent(
    Guid Id,
    DateTime OccurredAt,
    Guid UserId,
    string Email,
    string DisplayName,
    DateTime DemoExpiresAt);

public sealed record DemoUserExpiredEvent(
    Guid Id,
    DateTime OccurredAt,
    Guid UserId);

/// <summary>Published by the household service after the demo household is seeded.
/// Finance service consumes this to seed shared household bills.</summary>
public sealed record DemoHouseholdSeededEvent(
    Guid Id,
    DateTime OccurredAt,
    Guid UserId,
    Guid HouseholdId);
