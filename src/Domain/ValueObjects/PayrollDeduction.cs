namespace Finance.Domain.ValueObjects;

/// <summary>
/// Represents a single voluntary payroll deduction line item stored on an
/// <see cref="Finance.Domain.Aggregates.IncomeSource"/>.
/// Tax deductions (Federal, State, FICA) are NOT stored here — they are engine-computed
/// from the income source's <see cref="TaxWithholdingProfile"/>.
/// </summary>
public sealed class PayrollDeduction
{
    public DeductionType Type { get; private set; }

    /// <summary>
    /// Display label for the deduction, e.g. "Blue Cross PPO", "401(k) 6%".
    /// Allows multiple deductions of the same type (e.g. two health plans).
    /// </summary>
    public string Label { get; private set; } = string.Empty;

    public DeductionCalculationMethod Method { get; private set; }

    /// <summary>
    /// The deduction value: a percentage (e.g. 6.0 = 6%) for <see cref="DeductionCalculationMethod.PercentOfGross"/>,
    /// or a flat amount per pay period for <see cref="DeductionCalculationMethod.FixedAmount"/>.
    /// </summary>
    public decimal Value { get; private set; }

    /// <summary>
    /// True when the deduction is part of an employer-sponsored benefit plan
    /// (e.g. employer-paid portion of health insurance). Informational only —
    /// the amount still reduces the employee's gross pay for budgeting purposes.
    /// </summary>
    public bool IsEmployerSponsored { get; private set; }

    /// <summary>
    /// How often this deduction occurs. Used to normalise fixed-amount deductions
    /// to a monthly equivalent when computing net pay.
    /// Defaults to <see cref="RecurrenceFrequency.Monthly"/>.
    /// </summary>
    public RecurrenceFrequency Frequency { get; private set; } = RecurrenceFrequency.Monthly;

    /// <summary>
    /// Whether this deduction reduces federal and state taxable wages (W-2 Box 1) before
    /// income-tax brackets are applied. When <c>null</c> is passed to <see cref="Create"/>,
    /// the value is inferred from the deduction type via
    /// <see cref="Finance.Application.Engines.TaxCalculator.IsPreTaxDeduction"/>.
    /// Use this to override the default for edge cases (e.g. a post-tax health plan,
    /// or a non-standard 401(k) arrangement).
    /// </summary>
    public bool IsTaxExempt { get; private set; }

    // Required by EF Core JSON serialisation — do not use directly.
    private PayrollDeduction() { }

    public static PayrollDeduction Create(
        DeductionType type,
        string label,
        DeductionCalculationMethod method,
        decimal value,
        bool isEmployerSponsored = false,
        RecurrenceFrequency frequency = RecurrenceFrequency.Monthly,
        bool? isTaxExempt = null)
    {
        if (string.IsNullOrWhiteSpace(label))
            throw new ArgumentException("Deduction label cannot be empty.", nameof(label));

        if (value <= 0)
            throw new ArgumentException("Deduction value must be positive.", nameof(value));

        if (method == DeductionCalculationMethod.PercentOfGross && value > 100)
            throw new ArgumentException("Percentage deduction cannot exceed 100%.", nameof(value));

        // Tax types belong to the engine, not voluntary deduction storage
        if (type == DeductionType.FederalIncomeTax || type == DeductionType.StateIncomeTax
            || type == DeductionType.SocialSecurity || type == DeductionType.Medicare)
            throw new ArgumentException(
                $"Tax deduction type '{type}' is engine-computed and cannot be stored as a voluntary deduction.",
                nameof(type));

        return new PayrollDeduction
        {
            Type = type,
            Label = label.Trim(),
            Method = method,
            Value = value,
            IsEmployerSponsored = isEmployerSponsored,
            Frequency = frequency,
            IsTaxExempt = isTaxExempt ?? type.IsPreTax(),
        };
    }

    /// <summary>Computes the deduction amount against the supplied gross pay for one pay period.</summary>
    public Money Compute(Money grossPay) => Method switch
    {
        DeductionCalculationMethod.PercentOfGross => Money.Create(
            Math.Round(grossPay.Amount * Value / 100m, 2), grossPay.Currency),
        DeductionCalculationMethod.FixedAmount => Money.Create(
            Math.Min(Value, grossPay.Amount), grossPay.Currency),
        _ => throw new InvalidOperationException($"Unsupported calculation method: {Method}"),
    };
}
