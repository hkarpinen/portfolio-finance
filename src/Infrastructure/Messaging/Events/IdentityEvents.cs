// Wire contract for identity events consumed from RabbitMQ.
//
// MassTransit derives the AMQP exchange name from the .NET FQN
// (`Namespace:TypeName`), so this namespace + each record name MUST exactly
// match what the identity service publishes from `Domain.Events` — otherwise
// the bindings end up on a different exchange and finance silently misses
// every user-projection event. Mirrors the same wire-contract file in the
// household and forum services.
namespace Domain.Events;

public sealed record UserRegistered(
    Guid Id,
    DateTime OccurredAt,
    Guid UserId,
    string Email,
    string DisplayName);

public sealed record UserProfileUpdated(
    Guid Id,
    DateTime OccurredAt,
    Guid UserId,
    string DisplayName,
    string? AvatarUrl);

public sealed record UserBanned(
    Guid Id,
    DateTime OccurredAt,
    Guid UserId,
    DateTime BannedAt);

public sealed record DemoUserCreated(
    Guid Id,
    DateTime OccurredAt,
    Guid UserId,
    string Email,
    string DisplayName,
    DateTime DemoExpiresAt);

public sealed record DemoUserExpired(
    Guid Id,
    DateTime OccurredAt,
    Guid UserId);
