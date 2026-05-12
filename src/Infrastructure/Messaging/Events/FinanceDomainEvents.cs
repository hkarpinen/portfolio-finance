namespace Infrastructure.Messaging.Events;

// Wire shapes for bills domain events published via the outbox to RabbitMQ.
// Must match the flat camelCase JSON produced by OutboxExtensions.AddToOutbox.

public sealed record FinanceHouseholdCreatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid HouseholdId,
    string Name,
    Guid OwnerId,
    string CurrencyCode);

public sealed record FinanceHouseholdOwnershipTransferredEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid HouseholdId,
    Guid NewOwnerId);

public sealed record FinanceHouseholdMemberJoinedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid MembershipId,
    Guid HouseholdId,
    Guid UserId,
    string Role);

public sealed record FinanceHouseholdMemberLeftEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid MembershipId,
    Guid HouseholdId,
    Guid UserId);

public sealed record FinanceHouseholdMemberRemovedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid MembershipId,
    Guid HouseholdId,
    Guid UserId);

public sealed record FinanceHouseholdMemberRoleChangedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid MembershipId,
    Guid HouseholdId,
    Guid UserId,
    string NewRole);

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
    Guid HouseholdId,
    Guid MembershipId,
    Guid UserId);
