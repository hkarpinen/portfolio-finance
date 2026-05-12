using Finance.Domain.ValueObjects;

namespace Finance.Application.Commands;

public sealed record CreateExpenseCommand(
    Guid UserId,
    string Title,
    decimal Amount,
    string Currency,
    string Category,
    DateTime DueDate,
    string? RecurrenceFrequency = null,
    DateTime? RecurrenceStartDate = null,
    DateTime? RecurrenceEndDate = null,
    string? Description = null);

public sealed record UpdateExpenseCommand(
    Guid ExpenseId,
    string Title,
    decimal Amount,
    string Currency,
    string Category,
    DateTime DueDate,
    string? RecurrenceFrequency = null,
    DateTime? RecurrenceStartDate = null,
    DateTime? RecurrenceEndDate = null,
    string? Description = null);

public sealed record DeleteExpenseCommand(Guid ExpenseId);

public sealed record CreateHouseholdExpenseCommand(
    Guid HouseholdId,
    string Title,
    decimal Amount,
    string Currency,
    ExpenseCategory Category,
    Guid CreatedBy,
    DateTime DueDate,
    RecurrenceFrequency? RecurrenceFrequency = null,
    DateTime? RecurrenceStartDate = null,
    DateTime? RecurrenceEndDate = null,
    string? Description = null);

public sealed record UpdateHouseholdExpenseCommand(
    Guid ExpenseId,
    Guid CallerId,
    string Title,
    decimal Amount,
    string Currency,
    ExpenseCategory Category,
    DateTime DueDate,
    RecurrenceFrequency? RecurrenceFrequency = null,
    DateTime? RecurrenceStartDate = null,
    DateTime? RecurrenceEndDate = null,
    string? Description = null);

public sealed record DeactivateExpenseCommand(Guid ExpenseId, Guid CallerId);

public sealed record UpsertSplitCommand(
    Guid? SplitId,
    Guid ExpenseId,
    Guid HouseholdId,
    Guid MembershipId,
    Guid UserId,
    decimal Amount,
    string Currency);

public sealed record RemoveSplitCommand(Guid SplitId, Guid CallerId);

public sealed record MarkExpensePaidCommand(
    Guid ExpenseId,
    Guid UserId,
    DateTime OccurrenceDate,
    string? TransactionReference = null);

public sealed record MarkExpenseUnpaidCommand(
    Guid ExpenseId,
    Guid UserId,
    DateTime OccurrenceDate);

public sealed record PaymentOccurrenceBody(DateTime OccurrenceDate);
public sealed record SplitEvenlyBody(IReadOnlyList<Guid> MembershipIds);
