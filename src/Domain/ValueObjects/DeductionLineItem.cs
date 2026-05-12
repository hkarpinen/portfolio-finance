namespace Finance.Domain.ValueObjects;

/// <summary>
/// A single computed payroll deduction line — either engine-estimated (tax) or voluntary.
/// Produced by <see cref="Finance.Domain.Engines.PayrollDeductionEngine.ComputeBreakdown"/>.
/// </summary>
public sealed record DeductionLineItem(
    string Type,
    string Label,
    bool IsEmployerSponsored,
    decimal Amount,
    string Currency);
