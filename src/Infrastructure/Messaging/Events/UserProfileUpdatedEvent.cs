namespace Infrastructure.Messaging.Events;

/// <summary>Must match the wire shape published by the Identity service outbox.</summary>
public sealed record UserProfileUpdatedEvent(
    Guid Id,
    DateTime OccurredAt,
    Guid UserId,
    string DisplayName,
    string? AvatarUrl);
