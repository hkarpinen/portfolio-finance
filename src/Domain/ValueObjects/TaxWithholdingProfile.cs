namespace Finance.Domain.ValueObjects;

public sealed class TaxWithholdingProfile
{
    public FilingStatus FilingStatus { get; private set; }

    // Two-letter state code. Empty string or "NONE" means no state income tax applies.
    public string StateCode { get; private set; } = string.Empty;

    // Each allowance reduces annual taxable income by ~$4,300.
    public int FederalAllowances { get; private set; }

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
