using Finance.Domain.ValueObjects;

namespace Finance.Domain.Events;

public record HouseholdMemberInvited(
    MembershipId MembershipId,
    HouseholdId HouseholdId,
    UserId InvitedBy,
    string InvitationCode) : DomainEvent;

public record HouseholdMemberJoined(
    MembershipId MembershipId,
    HouseholdId HouseholdId,
    UserId UserId,
    HouseholdRole Role) : DomainEvent;

public record HouseholdMemberLeft(
    MembershipId MembershipId,
    HouseholdId HouseholdId,
    UserId UserId) : DomainEvent;

public record HouseholdMemberRoleChanged(
    MembershipId MembershipId,
    HouseholdId HouseholdId,
    UserId UserId,
    HouseholdRole NewRole) : DomainEvent;

public record HouseholdMemberRemoved(
    MembershipId MembershipId,
    HouseholdId HouseholdId,
    UserId UserId) : DomainEvent;
