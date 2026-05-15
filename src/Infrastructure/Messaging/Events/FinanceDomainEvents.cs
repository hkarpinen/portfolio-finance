namespace Infrastructure.Messaging.Events;

// Wire shapes for finance domain events published via the outbox to RabbitMQ.
// Must match the flat camelCase JSON produced by OutboxExtensions.AddToOutbox.

public sealed record FinanceExpenseCreatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ExpenseId,
    Guid? HouseholdId,
    string Title,
    Guid CreatedBy);

public sealed record FinanceExpenseSplitCreatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ExpenseSplitId,
    Guid ExpenseId,
    Guid GroupId,
    Guid UserId);
