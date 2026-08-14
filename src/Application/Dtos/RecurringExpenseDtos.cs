using Finance.Domain.ValueObjects;

namespace Finance.Application.Dtos;

public sealed record RecurringExpenseDto(
    Guid RecurringExpenseId,
    Guid? GroupId,
    string Title,
    string? Description,
    decimal Amount,
    string Currency,
    string Category,
    string Frequency,
    DateTime AnchorDate,
    DateTime? EndDate,
    Guid? PayerUserId,
    string FundingSource,
    bool IsActive);

/// <summary>
/// One date the schedule places a expense on, and whether that expense exists yet.
///
/// Unrecorded is the normal state: a expense is only written when somebody acts on it, so most of
/// what a screen shows is forecast — real dates and amounts, no rows behind them.
/// </summary>
public sealed record ScheduledOccurrenceDto(
    DateTime OccurrenceDate,
    decimal Amount,
    string Currency,
    Guid? ExpenseId);

public sealed record CreateRecurringExpenseCommand(
    Guid? GroupId,
    Guid CallerUserId,
    string Title,
    decimal Amount,
    string Currency,
    ExpenseCategory Category,
    RecurrenceFrequency Frequency,
    DateTime AnchorDate,
    DateTime? EndDate = null,
    string? Description = null,
    Guid? PayerUserId = null,
    FundingSource FundingSource = FundingSource.PayerMember);

/// <summary>
/// `EffectiveFrom` null means from today. Set it to back-date a rise that already happened —
/// occurrences before it keep the older amount, which is what makes this a versioned agreement
/// rather than an edit.
/// </summary>
public sealed record AmendRecurringExpenseCommand(
    Guid RecurringExpenseId,
    Guid CallerUserId,
    string Title,
    decimal Amount,
    string Currency,
    ExpenseCategory Category,
    string? Description = null,
    DateTime? EffectiveFrom = null);
