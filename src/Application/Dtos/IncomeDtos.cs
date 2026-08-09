using Finance.Domain.ValueObjects;

namespace Finance.Application.Dtos;

public sealed record TaxProfileDto(
    FilingStatus FilingStatus,
    string StateCode,
    int FederalAllowances,
    int StateAllowances);

public sealed record PayrollDeductionDto(
    DeductionType Type,
    string Label,
    DeductionCalculationMethod Method,
    decimal Value,
    bool IsEmployerSponsored,
    RecurrenceFrequency Frequency = RecurrenceFrequency.Monthly,
    bool IsTaxExempt = false);

public sealed record IncomeDto(
    Guid IncomeId,
    Guid UserId,
    decimal Amount,
    string Currency,
    string Source,
    RecurrenceFrequency QuotedAs,
    // Equals QuotedAs when not separately specified.
    RecurrenceFrequency PaidEvery,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive,
    DateTime? LastPaycheckDate,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    TaxProfileDto? TaxProfile = null,
    IReadOnlyList<PayrollDeductionDto>? Deductions = null,
    string? Notes = null);

public sealed record IncomeListDto(IReadOnlyCollection<IncomeDto> Items, int TotalCount);

// Type is a string here, not the DeductionType enum, because the payroll engine produces
// engine-only categories ("SocialSecurity", "Medicare") that are not user-selectable.
public sealed record DeductionLineItemDto(
    string Type,
    string Label,
    bool IsEmployerSponsored,
    decimal Amount,
    string Currency);

public sealed record NetPayBreakdownDto(
    Guid IncomeId,
    decimal GrossPay,
    string Currency,
    IReadOnlyList<DeductionLineItemDto> Deductions,
    decimal TotalDeductions,
    decimal NetPay);

// Currency is the currency of the largest-grossing source. Sources in other currencies are still
// summed into the totals raw, with no FX — a mixed-currency total is therefore meaningless and
// only the per-row figures are safe to show.
public sealed record NetPaySummaryDto(
    int Year,
    int Month,
    string Currency,
    decimal MonthlyGross,
    decimal MonthlyNet,
    decimal TotalTaxWithheld,
    decimal TotalDeductions,
    decimal AnnualGross,
    int SourceCount);
