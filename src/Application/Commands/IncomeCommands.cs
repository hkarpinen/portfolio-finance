using Finance.Application.Dtos;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Commands;

public sealed record CreateIncomeCommand(
    Guid UserId,
    decimal Amount,
    string Currency,
    string Source,
    RecurrenceFrequency QuotedAs,
    // Defaults to QuotedAs.
    RecurrenceFrequency? PaidEvery,
    DateTime StartDate,
    // Anchors the recurrence, so exact pay dates fall on the real cadence rather than the start date.
    DateTime? LastPaycheckDate = null,
    DateTime? EndDate = null,
    IReadOnlyList<PayrollDeductionDto>? InitialDeductions = null,
    string? Notes = null);

// CallerUserId is the owner check and is always overwritten server-side from the token.
public sealed record UpdateIncomeCommand(
    Guid CallerUserId,
    Guid IncomeId,
    decimal Amount,
    string Currency,
    string Source,
    RecurrenceFrequency QuotedAs,
    // Defaults to QuotedAs.
    RecurrenceFrequency? PaidEvery,
    DateTime StartDate,
    // Anchors the recurrence.
    DateTime? LastPaycheckDate = null,
    DateTime? EndDate = null,
    string? Notes = null);

// CallerUserId is the owner check, always overwritten in the controller from the token.
public sealed record SetTaxProfileCommand(
    Guid IncomeId,
    // Null CLEARS the tax profile.
    TaxProfileDto? TaxProfile,
    Guid CallerUserId = default);

public sealed record AddDeductionCommand(
    Guid IncomeId,
    PayrollDeductionDto Deduction,
    Guid CallerUserId = default);

public sealed record RemoveDeductionCommand(
    Guid IncomeId,
    string DeductionType,
    string Label,
    Guid CallerUserId = default);

public sealed record DeleteIncomeCommand(Guid IncomeId);

// CallerUserId is the owner check, always overwritten server-side from the token.
public sealed record DeactivateIncomeCommand(Guid IncomeId, Guid CallerUserId);
