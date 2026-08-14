// Wire contract for household events consumed from RabbitMQ.
//
// MassTransit derives the AMQP exchange name from the .NET FQN (`Namespace:TypeName`), so this
// namespace and these record names MUST exactly match what the publisher declares — otherwise the
// binding lands on a different exchange and every message is missed silently. Fields are plain
// Guid/decimal/string (no value objects) so the default camelCase deserializer handles them
// without custom converters.
namespace Household.Domain.Events;

// UserId is authoritative — the publisher has already verified the caller may act for that
// member, so finance upserts the share without re-checking roles. GroupId == the household id.
public sealed record GroupShareAssigned(
    Guid Id,
    DateTime OccurredAt,
    Guid GroupId,
    Guid ExpenseId,
    Guid UserId,
    decimal Amount,
    string Currency);

// These carry no event id, so consumers dedup on the transport MessageId instead. A member's
// ledger accounts and balances deliberately survive their departure — debt does not vanish when
// someone leaves — so the projection is what lets read models tell current members from departed.

public sealed record HouseholdMemberJoined(
    Guid MembershipId,
    Guid HouseholdId,
    Guid UserId,
    string Role,
    DateTime JoinedAt);

public sealed record HouseholdMemberLeft(
    Guid MembershipId,
    Guid HouseholdId,
    Guid UserId,
    DateTime LeftAt);

public sealed record HouseholdMemberRemoved(
    Guid MembershipId,
    Guid HouseholdId,
    Guid RemovedByUserId,
    Guid RemovedUserId,
    DateTime RemovedAt);

public sealed record HouseholdMemberRoleChanged(
    Guid MembershipId,
    Guid HouseholdId,
    Guid UserId,
    string OldRole,
    string NewRole,
    DateTime ChangedAt);

// Raised for a real deletion or a demo expiry. GroupId == the household id. No event id — dedup
// on the transport MessageId.
public sealed record HouseholdDeleted(
    Guid HouseholdId,
    DateTime DeletedAt);
