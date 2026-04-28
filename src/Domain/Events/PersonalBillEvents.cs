using Bills.Domain.ValueObjects;

namespace Bills.Domain.Events;

public record PersonalBillCreated(
    PersonalBillId PersonalBillId,
    UserId UserId,
    string Title,
    Money Amount,
    BillCategory Category,
    DateTime DueDate,
    RecurrenceSchedule? RecurrenceSchedule) : DomainEvent;

public record PersonalBillUpdated(
    PersonalBillId PersonalBillId,
    string Title,
    Money Amount,
    BillCategory Category,
    DateTime DueDate) : DomainEvent;

public record PersonalBillDeactivated(
    PersonalBillId PersonalBillId) : DomainEvent;
