namespace Finance.Domain.ValueObjects;

/// <summary>
/// Captures the information needed to estimate federal and state income tax withholding
/// for a given income source. Owned by <see cref="Finance.Domain.Aggregates.IncomeSource"/>.
/// </summary>
public sealed class TaxWithholdingProfile
{
    public FilingStatus FilingStatus { get; private set; }

    /// <summary>
    /// Two-letter state code (e.g. "CA", "NY"). Empty string or "NONE" means no state income tax applies.
    /// </summary>
    public string StateCode { get; private set; } = string.Empty;

    /// <summary>Number of federal withholding allowances claimed (each reduces annual taxable income by ~$4,300).</summary>
    public int FederalAllowances { get; private set; }

    /// <summary>Number of state withholding allowances claimed.</summary>
    public int StateAllowances { get; private set; }

    // Required by EF Core — do not use directly.
    private TaxWithholdingProfile() { }

    public static TaxWithholdingProfile Create(
        FilingStatus filingStatus,
        string stateCode,
        int federalAllowances = 0,
        int stateAllowances = 0)
    {
        if (federalAllowances < 0) throw new ArgumentException("Federal allowances cannot be negative.", nameof(federalAllowances));
        if (stateAllowances < 0) throw new ArgumentException("State allowances cannot be negative.", nameof(stateAllowances));

        return new TaxWithholdingProfile
        {
            FilingStatus = filingStatus,
            StateCode = (stateCode ?? string.Empty).Trim().ToUpperInvariant(),
            FederalAllowances = federalAllowances,
            StateAllowances = stateAllowances,
        };
    }
}
