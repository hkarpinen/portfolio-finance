// Wire contract for household events consumed from RabbitMQ.
//
// MassTransit derives the AMQP exchange name from the .NET FQN (`Namespace:TypeName`),
// so this namespace + record name MUST exactly match what the household service publishes
// from `Household.Domain.Events` — otherwise the binding lands on a different exchange and
// finance silently misses every message. Mirrors the same wire-contract pattern as
// IdentityEvents.cs. Fields are plain Guid/decimal/string (no value objects) so the default
// camelCase deserializer handles them without custom converters.
namespace Household.Domain.Events;

/// <summary>
/// Household has authorized (role-checked) a member's allocation on a finance charge. Finance
/// upserts the allocation; <c>UserId</c> is authoritative (household already verified the caller
/// may act for that member). This is the async, no-service-to-service path for the role-gated
/// "add a split for another member". <c>GroupId</c> == the household id.
/// </summary>
public sealed record GroupAllocationAssigned(
    Guid Id,
    DateTime OccurredAt,
    Guid GroupId,
    Guid ChargeId,
    Guid UserId,
    decimal Amount,
    string Currency);

// ── Membership lifecycle ─────────────────────────────────────────────────────
// Household's member events carry no event id (its DomainEvent base is an empty marker), so
// consumers dedup on the transport MessageId instead. Finance keeps a GroupMemberProjection
// from these: who is (still) in a group and with what role. A member's ledger accounts and
// balances deliberately survive their departure — debt does not vanish when someone leaves —
// the projection is what lets read models tell current members from departed ones.

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

/// <summary>
/// A household was deleted (real deletion or demo expiry). Finance consumes this to tear down the
/// group's derived state — its ledger graph (ledger, accounts, journal entries, postings), member
/// projections, and charges/allocations — so a gone household leaves no orphan book behind.
/// <c>GroupId</c> == the household id. No event id — dedup on the transport MessageId.
/// </summary>
public sealed record HouseholdDeleted(
    Guid HouseholdId,
    DateTime DeletedAt);
