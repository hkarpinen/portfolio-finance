namespace Finance.Domain.ValueObjects;

/// <summary>Voluntary deductions only — tax is computed, never stored as one of these.</summary>
public sealed class PayrollDeduction
{
    public DeductionType Type { get; private set; }

    /// <summary>Free-form, so a member can hold two deductions of the same type.</summary>
    public string Label { get; private set; } = string.Empty;

    public DeductionCalculationMethod Method { get; private set; }

    /// <summary>
    /// Meaning depends on the method: a PERCENT (6.0 = 6%) for PercentOfGross, or a
    /// flat per-period amount for FixedAmount.
    /// </summary>
    public decimal Value { get; private set; }

    /// <summary>Informational only — the amount still reduces gross pay either way.</summary>
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
    /// <see cref="Finance.Domain.Engines.TaxCalculator.IsPreTaxDeduction"/>.
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
