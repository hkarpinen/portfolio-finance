namespace Infrastructure.Messaging.Events;

/// <summary>Must match the wire shape published by the Identity service outbox.</summary>
public sealed record UserBannedEvent(
    Guid Id,
    DateTime OccurredAt,
    Guid UserId,
    DateTime BannedAt);
