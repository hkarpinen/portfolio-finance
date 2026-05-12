namespace Finance.Domain.ValueObjects;

/// <summary>
/// Monthly net pay breakdown produced by
/// <see cref="Finance.Domain.Engines.PayrollDeductionEngine.ComputeBreakdown"/>.
/// </summary>
public sealed record NetPayBreakdown(
    Guid IncomeId,
    decimal GrossPay,
    string Currency,
    IReadOnlyList<DeductionLineItem> Deductions,
    decimal TotalDeductions,
    decimal NetPay);
