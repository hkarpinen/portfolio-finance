namespace Infrastructure.Messaging.Events;

/// <summary>Must match the wire shape published by the Identity service outbox.</summary>
public sealed record UserRegisteredEvent(
    Guid Id,
    DateTime OccurredAt,
    Guid UserId,
    string Email,
    string DisplayName);
