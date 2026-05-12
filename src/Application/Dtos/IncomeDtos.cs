using Finance.Domain.ValueObjects;

namespace Finance.Application.Dtos;

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
    IReadOnlyList<PayrollDeductionDto>? Deductions = null);

public sealed record IncomeListDto(IReadOnlyCollection<IncomeDto> Items, int TotalCount);

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
