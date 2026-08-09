namespace Finance.Domain.ValueObjects;

public sealed record NetPayBreakdown(
    Guid IncomeId,
    decimal GrossPay,
    string Currency,
    IReadOnlyList<DeductionLineItem> Deductions,
    decimal TotalDeductions,
    decimal NetPay);
