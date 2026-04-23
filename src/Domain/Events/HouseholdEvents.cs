using Bills.Domain.ValueObjects;

namespace Bills.Domain.Events;

public record HouseholdCreated(
    HouseholdId HouseholdId,
    string Name,
    UserId OwnerId,
    string CurrencyCode) : DomainEvent;

public record HouseholdUpdated(
    HouseholdId HouseholdId,
    string Name) : DomainEvent;

public record HouseholdDeleted(
    HouseholdId HouseholdId) : DomainEvent;

public record HouseholdOwnershipTransferred(
    HouseholdId HouseholdId,
    UserId NewOwnerId) : DomainEvent;
