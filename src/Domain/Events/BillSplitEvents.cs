using Bills.Domain.ValueObjects;

namespace Bills.Domain.Events;

public record BillSplitCreated(
    SplitId SplitId,
    BillId BillId,
    HouseholdId HouseholdId,
    MembershipId MembershipId,
    UserId UserId,
    Money Amount) : DomainEvent;

public record BillSplitClaimed(
    SplitId SplitId,
    BillId BillId,
    HouseholdId HouseholdId,
    MembershipId MembershipId,
    Money Amount) : DomainEvent;

public record BillSplitUpdated(
    SplitId SplitId,
    BillId BillId,
    HouseholdId HouseholdId,
    Money NewAmount) : DomainEvent;

public record BillSplitRemoved(
    SplitId SplitId,
    BillId BillId,
    HouseholdId HouseholdId,
    MembershipId MembershipId) : DomainEvent;
