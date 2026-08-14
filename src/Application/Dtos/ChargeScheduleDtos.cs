using Finance.Domain.ValueObjects;

namespace Finance.Application.Dtos;

public sealed record ChargeScheduleDto(
    Guid ScheduleId,
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
/// One date the schedule places a charge on, and whether that charge exists yet.
///
/// Unrecorded is the normal state: a charge is only written when somebody acts on it, so most of
/// what a screen shows is forecast — real dates and amounts, no rows behind them.
/// </summary>
public sealed record ScheduledOccurrenceDto(
    DateTime OccurrenceDate,
    decimal Amount,
    string Currency,
    Guid? ChargeId);

public sealed record CreateChargeScheduleCommand(
    Guid? GroupId,
    Guid CallerUserId,
    string Title,
    decimal Amount,
    string Currency,
    ChargeCategory Category,
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
public sealed record AmendChargeScheduleCommand(
    Guid ScheduleId,
    Guid CallerUserId,
    string Title,
    decimal Amount,
    string Currency,
    ChargeCategory Category,
    string? Description = null,
    DateTime? EffectiveFrom = null);
