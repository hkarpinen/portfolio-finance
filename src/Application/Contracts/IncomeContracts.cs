using Finance.Domain.ValueObjects;

namespace Finance.Application.Contracts;

// ── Request DTOs ─────────────────────────────────────────────────────────────

public sealed record TaxProfileDto(
    string FilingStatus,
    string StateCode,
    int FederalAllowances,
    int StateAllowances);

public sealed record PayrollDeductionDto(
    string Type,
    string Label,
    string Method,
    decimal Value,
    bool IsEmployerSponsored,
    string Frequency = "Monthly",
    bool IsTaxExempt = false);

public sealed record CreateIncomeRequest(
    Guid UserId,
    decimal Amount,
    string Currency,
    string Source,
    RecurrenceFrequency Frequency,
    DateTime StartDate,
    DateTime? EndDate = null,
    DateTime? LastPaymentDate = null,
    IReadOnlyList<PayrollDeductionDto>? InitialDeductions = null);

public sealed record UpdateIncomeRequest(
    Guid IncomeId,
    decimal Amount,
    string Currency,
    string Source,
    RecurrenceFrequency Frequency,
    DateTime StartDate,
    DateTime? EndDate = null,
    DateTime? LastPaymentDate = null);

public sealed record SetTaxProfileRequest(
    Guid IncomeId,
    /// <summary>Null to clear the tax profile.</summary>
    TaxProfileDto? TaxProfile);

public sealed record AddDeductionRequest(
    Guid IncomeId,
    PayrollDeductionDto Deduction);

public sealed record RemoveDeductionRequest(
    Guid IncomeId,
    string DeductionType,
    string Label);

public sealed record GetNetPayBreakdownRequest(
    Guid IncomeId,
    int Year,
    int Month);

public sealed record IncomeDetailRequest(Guid IncomeId);

public sealed record DeleteIncomeRequest(Guid IncomeId);

public sealed record DeactivateIncomeRequest(Guid IncomeId);

public sealed record ListIncomeRequest(
    Guid HouseholdId,
    int Page = 1,
    int PageSize = 20,
    bool ActiveOnly = true);

public sealed record ListUserIncomeRequest(
    Guid UserId,
    int Page = 1,
    int PageSize = 20,
    bool ActiveOnly = true);

// ── Response DTOs ────────────────────────────────────────────────────────────

public sealed record IncomeResponse(
    Guid IncomeId,
    Guid UserId,
    decimal Amount,
    string Currency,
    string Source,
    RecurrenceFrequency Frequency,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive,
    DateTime? LastPaymentDate,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    TaxProfileDto? TaxProfile = null,
    IReadOnlyList<PayrollDeductionDto>? Deductions = null);

public sealed record IncomeListResponse(IReadOnlyCollection<IncomeResponse> Items, int TotalCount);

// ── Net Pay Breakdown ────────────────────────────────────────────────────────

public sealed record DeductionLineItemDto(
    string Type,
    string Label,
    bool IsEmployerSponsored,
    decimal Amount,
    string Currency);

public sealed record NetPayBreakdownResponse(
    Guid IncomeId,
    decimal GrossPay,
    string Currency,
    IReadOnlyList<DeductionLineItemDto> Deductions,
    decimal TotalDeductions,
    decimal NetPay);
