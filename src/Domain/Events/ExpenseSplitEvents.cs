using Finance.Domain.ValueObjects;

namespace Finance.Domain.Events;

public record ExpenseSplitCreated(
    ExpenseSplitId ExpenseSplitId,
    ExpenseId ExpenseId,
    GroupId GroupId,
    UserId UserId,
    Money Amount) : DomainEvent;

public record ExpenseSplitUpdated(
    ExpenseSplitId ExpenseSplitId,
    ExpenseId ExpenseId,
    GroupId GroupId,
    Money NewAmount) : DomainEvent;

public record ExpenseSplitRemoved(
    ExpenseSplitId ExpenseSplitId,
    ExpenseId ExpenseId,
    GroupId GroupId) : DomainEvent;

public record ExpenseSplitPaid(
    ExpenseSplitId ExpenseSplitId,
    ExpenseId ExpenseId,
    GroupId GroupId,
    UserId UserId,
    DateTime OccurrenceDate) : DomainEvent;

public record ExpenseSplitUnpaid(
    ExpenseSplitId ExpenseSplitId,
    ExpenseId ExpenseId,
    GroupId GroupId,
    UserId UserId,
    DateTime OccurrenceDate) : DomainEvent;

