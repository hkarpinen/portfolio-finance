namespace Finance.Domain.ValueObjects;

/// <summary>How a <see cref="PayrollDeduction"/> amount is computed.</summary>
public enum DeductionCalculationMethod
{
    /// <summary>The deduction value is a percentage of gross pay (e.g. 6 = 6%).</summary>
    PercentOfGross,

    /// <summary>The deduction value is a fixed dollar (or native-currency) amount per pay period.</summary>
    FixedAmount,
}
