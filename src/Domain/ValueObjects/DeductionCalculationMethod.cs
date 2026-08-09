namespace Finance.Domain.ValueObjects;

public enum DeductionCalculationMethod
{
    // Value is a percentage of gross pay: 6 means 6%.
    PercentOfGross,

    // Value is a fixed amount PER PAY PERIOD, not per month.
    FixedAmount,
}
