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
    /// <summary>The period the Amount is quoted in.</summary>
    RecurrenceFrequency QuotedAs,
    /// <summary>How often a paycheck actually arrives. Equals QuotedAs when not separately specified.</summary>
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

// Type is a string here (not the DeductionType enum) because PayrollDeductionEngine
// produces engine-only categories like "SocialSecurity" and "Medicare" that aren't
// part of the user-selectable DeductionType enum.
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

/// <summary>
/// Aggregated net-pay figures across every active income source for the
/// caller. Served by the income page's stats strip so the UI does not have
/// to fan out a separate /net-pay call per source. `Currency` is the
/// currency of the largest-grossing source — sources in other currencies
/// are still summed (raw, no FX) and the UI is expected to handle the
/// mixed-currency edge case at the row level.
/// </summary>
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
