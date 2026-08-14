using System.Text.Json.Serialization;
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
/// One date the schedule places an expense on, and whether that expense exists yet.
///
/// Unrecorded is the normal state: an expense is only written when somebody acts on it, so most of
/// what a screen shows is forecast — real dates and amounts, no rows behind them.
/// </summary>
public sealed record ScheduledOccurrenceDto(
    DateTime OccurrenceDate,
    decimal Amount,
    string Currency,
    Guid? ExpenseId);

/// <summary>
/// `GroupId` names the group the agreement belongs to, or null for one of your own. It is the
/// one id here the body legitimately supplies — there is no `{groupId}` in this route for the
/// membership filter to work from — so the manager checks membership itself before opening it.
/// </summary>
public sealed record CreateRecurringExpenseCommand(
    Guid? GroupId,
    [property: JsonIgnore] Guid CallerUserId,
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
    [property: JsonIgnore] Guid RecurringExpenseId,
    [property: JsonIgnore] Guid CallerUserId,
    string Title,
    decimal Amount,
    string Currency,
    ExpenseCategory Category,
    string? Description = null,
    DateTime? EffectiveFrom = null);
