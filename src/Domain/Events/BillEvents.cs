using Finance.Domain.ValueObjects;

namespace Finance.Domain.Events;

public record BillCreated(
    BillId BillId,
    HouseholdId HouseholdId,
    string Title,
    Money Amount,
    BillCategory Category,
    UserId CreatedBy,
    DateTime DueDate,
    RecurrenceSchedule? RecurrenceSchedule) : DomainEvent;

public record BillUpdated(
    BillId BillId,
    HouseholdId HouseholdId,
    string Title,
    Money Amount,
    BillCategory Category,
    DateTime DueDate) : DomainEvent;

public record BillDeactivated(
    BillId BillId,
    HouseholdId HouseholdId) : DomainEvent;
