using Finance.Application.Dtos;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Commands;

public sealed record CreateIncomeCommand(
    Guid UserId,
    decimal Amount,
    string Currency,
    string Source,
    /// <summary>The period the Amount is quoted in (e.g. Annually for a $80k salary).</summary>
    RecurrenceFrequency QuotedAs,
    /// <summary>How often a paycheck actually arrives (e.g. BiWeekly). Defaults to QuotedAs.</summary>
    RecurrenceFrequency? PaidEvery,
    DateTime StartDate,
    /// <summary>Date of the most recent paycheck — used as the recurrence anchor for exact pay-date calculation.</summary>
    DateTime? LastPaycheckDate = null,
    DateTime? EndDate = null,
    IReadOnlyList<PayrollDeductionDto>? InitialDeductions = null);

public sealed record UpdateIncomeCommand(
    Guid IncomeId,
    decimal Amount,
    string Currency,
    string Source,
    /// <summary>The period the Amount is quoted in.</summary>
    RecurrenceFrequency QuotedAs,
    /// <summary>How often a paycheck actually arrives. Defaults to QuotedAs.</summary>
    RecurrenceFrequency? PaidEvery,
    DateTime StartDate,
    /// <summary>Date of the most recent paycheck — recurrence anchor.</summary>
    DateTime? LastPaycheckDate = null,
    DateTime? EndDate = null);

public sealed record SetTaxProfileCommand(
    Guid IncomeId,
    /// <summary>Null to clear the tax profile.</summary>
    TaxProfileDto? TaxProfile);

public sealed record AddDeductionCommand(
    Guid IncomeId,
    PayrollDeductionDto Deduction);

public sealed record RemoveDeductionCommand(
    Guid IncomeId,
    string DeductionType,
    string Label);

public sealed record DeleteIncomeCommand(Guid IncomeId);

public sealed record DeactivateIncomeCommand(Guid IncomeId);
