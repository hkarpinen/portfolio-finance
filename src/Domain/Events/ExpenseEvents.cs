using Finance.Domain.ValueObjects;

namespace Finance.Domain.Events;

public record ExpenseCreated(
    ExpenseId ExpenseId,
    UserId UserId,
    string Title,
    Money Amount,
    ExpenseCategory Category,
    DateTime DueDate,
    GroupId? GroupId = null,
    Guid? PayerUserId = null) : DomainEvent;

public record ExpenseUpdated(
    ExpenseId ExpenseId,
    string Title,
    Money Amount,
    ExpenseCategory Category,
    DateTime DueDate,
    GroupId? GroupId = null,
    Guid? PayerUserId = null) : DomainEvent;

public record ExpenseDeactivated(
    ExpenseId ExpenseId,
    GroupId? GroupId = null) : DomainEvent;

public record ExpensePaid(
    ExpenseId ExpenseId,
    UserId UserId,
    DateTime OccurrenceDate,
    DateTime PaidAt) : DomainEvent;

public record ExpenseActivated(
    ExpenseId ExpenseId,
    GroupId? GroupId = null) : DomainEvent;

public record ExpenseUnpaid(
    ExpenseId ExpenseId,
    UserId UserId,
    DateTime OccurrenceDate) : DomainEvent;

public record VendorPaid(
    ExpenseId ExpenseId,
    DateTime OccurrenceDate,
    FundingSource FundingSource,
    Guid? PaidByUserId,
    DateTime PaidAt) : DomainEvent;

public record VendorPaymentReversed(
    ExpenseId ExpenseId,
    DateTime OccurrenceDate) : DomainEvent;
