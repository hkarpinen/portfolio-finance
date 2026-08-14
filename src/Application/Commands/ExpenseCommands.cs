using System.Text.Json.Serialization;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Commands;

public sealed record CreateExpenseCommand(
    [property: JsonIgnore] Guid CallerUserId,
    string Title,
    decimal Amount,
    string Currency,
    string Category,
    DateTime DueDate,
    string? RecurrenceFrequency = null,
    DateTime? RecurrenceStartDate = null,
    DateTime? RecurrenceEndDate = null,
    string? Description = null,
    /// <summary>Which of the caller's accounts settles it — a card, or checking. Null means cash,
    /// which is the honest default for somebody who has told us about no accounts.</summary>
    Guid? FundingAccountId = null);

// Anything the server already knows is marked JsonIgnore: who is asking comes from the token, and
// the group comes from the route. A body that names either is not a bad request the binder can
// reject — it is a well-formed one saying something it has no standing to say, so the binder is
// simply not allowed to read them.
// CallerUserId is WHO IS ASKING, on every command that changes something. An expense id on its own
// must never authorise an update, a delete or a re-split — the aggregate decides, via
// Expense.IsManagedBy.
//
// Anyone the operation is ABOUT keeps a role name instead: MemberUserId, PayerUserId. The two were
// both called UserId, which meant the same word was the actor on one command and the subject on
// the next, and nothing but the manager could tell you which.
public sealed record UpdateExpenseCommand(
    Guid ExpenseId,
    [property: JsonIgnore] Guid CallerUserId,
    string Title,
    decimal Amount,
    string Currency,
    string Category,
    DateTime DueDate,
    // No cadence here. Amending a bill amends that one bill; how often it comes round is the
    // schedule's business, and a copy on the bill is what let the two disagree.
    string? Description = null);

public sealed record DeleteExpenseCommand(Guid ExpenseId, Guid CallerUserId);

/// <summary>Whose share it is — an actor never appears in a nested DTO.</summary>
public sealed record CreateShareDto(Guid MemberUserId, decimal Amount, string Currency);

public sealed record CreateGroupExpenseCommand(
    [property: JsonIgnore] Guid GroupId,
    string Title,
    decimal Amount,
    string Currency,
    ExpenseCategory Category,
    [property: JsonIgnore] Guid CallerUserId,
    DateTime DueDate,
    RecurrenceFrequency? RecurrenceFrequency = null,
    DateTime? RecurrenceStartDate = null,
    DateTime? RecurrenceEndDate = null,
    string? Description = null,
    Guid? PayerUserId = null,
    FundingSource FundingSource = FundingSource.PayerMember,
    IReadOnlyList<CreateShareDto>? Shares = null);

public sealed record UpdateGroupExpenseCommand(
    Guid ExpenseId,
    [property: JsonIgnore] Guid CallerUserId,
    string Title,
    decimal Amount,
    string Currency,
    ExpenseCategory Category,
    DateTime DueDate,
    string? Description = null,
    Guid? PayerUserId = null);

public sealed record DeactivateExpenseCommand(Guid ExpenseId, Guid CallerUserId);

public sealed record UpsertShareCommand(
    Guid? ShareId,
    Guid ExpenseId,
    [property: JsonIgnore] Guid GroupId,
    [property: JsonIgnore] Guid CallerUserId,
    /// <summary>Whose share this is — not who is assigning it.</summary>
    Guid MemberUserId,
    decimal Amount,
    string Currency);

public sealed record RemoveShareCommand(Guid ShareId, Guid CallerUserId);

public sealed record MarkExpensePaidCommand(
    Guid ExpenseId,
    [property: JsonIgnore] Guid CallerUserId,
    DateTime OccurrenceDate,
    string? TransactionReference = null,
    /// <summary>Which account the money came from. Null keeps whatever the expense already named,
    /// and failing that, cash.</summary>
    Guid? FundingAccountId = null);

public sealed record MarkExpenseUnpaidCommand(
    Guid ExpenseId,
    [property: JsonIgnore] Guid CallerUserId,
    DateTime OccurrenceDate);

// Owner-only: the bill's owner pays the vendor from the shared pot (collect-first).
public sealed record MarkVendorPaidCommand(
    Guid ExpenseId,
    [property: JsonIgnore] Guid CallerUserId,
    DateTime OccurrenceDate);

public sealed record MarkVendorUnpaidCommand(
    Guid ExpenseId,
    [property: JsonIgnore] Guid CallerUserId,
    DateTime OccurrenceDate);

public sealed record PaymentOccurrenceBody(DateTime OccurrenceDate);
public sealed record SettleUpTransferBody(Guid ToUserId, decimal Amount, string Currency);
public sealed record AllocateEvenlyBody(IReadOnlyList<Guid> UserIds);
