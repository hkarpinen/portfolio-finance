using Finance.Domain.ValueObjects;

namespace Finance.Domain.Events;

public record ExpenseCreated(
    ExpenseId ExpenseId,
    UserId UserId,
    string Title,
    Money Amount,
    ExpenseCategory Category,
    DateTime DueDate,
    RecurrenceSchedule? RecurrenceSchedule,
    GroupId? HouseholdId = null) : DomainEvent;

public record ExpenseUpdated(
    ExpenseId ExpenseId,
    string Title,
    Money Amount,
    ExpenseCategory Category,
    DateTime DueDate,
    GroupId? HouseholdId = null) : DomainEvent;

public record ExpenseDeactivated(
    ExpenseId ExpenseId,
    GroupId? HouseholdId = null) : DomainEvent;

public record ExpensePaid(
    ExpenseId ExpenseId,
    UserId UserId,
    DateTime OccurrenceDate,
    DateTime PaidAt) : DomainEvent;

public record ExpenseUnpaid(
    ExpenseId ExpenseId,
    UserId UserId,
    DateTime OccurrenceDate) : DomainEvent;
