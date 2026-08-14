namespace Finance.Domain.Events;

public sealed record RecurringExpenseCreated(
    Guid RecurringExpenseId,
    Guid UserId,
    Guid? GroupId,
    string Title,
    decimal Amount,
    string Currency,
    string Category,
    string Frequency,
    DateTime AnchorDate,
    DateTime OccurredAt) : DomainEvent;

public sealed record RecurringExpenseAmended(
    Guid RecurringExpenseId,
    string Title,
    decimal Amount,
    string Currency,
    string Category,
    DateTime OccurredAt) : DomainEvent;

public sealed record RecurringExpenseDeactivated(
    Guid RecurringExpenseId,
    DateTime OccurredAt) : DomainEvent;
