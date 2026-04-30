namespace Finance.Domain.ValueObjects;

/// <summary>
/// Categorises a payroll deduction line item.
/// Tax types (Federal/State/FICA) are computed by the PayrollDeductionEngine from
/// the income source's TaxWithholdingProfile.
/// Voluntary types are stored on the aggregate and computed from their declared method/value.
/// </summary>
public enum DeductionType
{
    // Note: IsPreTax() extension below is the single source of truth for pre-tax classification.
    // ── Tax withholding (engine-computed) ───────────────────────────────────
    FederalIncomeTax,
    StateIncomeTax,
    SocialSecurity,
    Medicare,

    // ── Employer-sponsored / voluntary benefits ─────────────────────────────
    HealthInsurance,
    DentalInsurance,
    VisionInsurance,
    LifeInsurance,
    Retirement401k,
    Roth401k,
    HSA,
    FSA,

    Other,
}

public static class DeductionTypeExtensions
{
    /// <summary>
    /// Returns true when the deduction type reduces federal/state taxable wages (W-2 Box 1)
    /// before income-tax brackets are applied (§125 cafeteria plan, §401(a), §106/125 HSA/FSA).
    /// </summary>
    public static bool IsPreTax(this DeductionType type) => type switch
    {
        DeductionType.Retirement401k  => true,   // §401(a) traditional 401(k)
        DeductionType.HealthInsurance => true,   // §125 cafeteria plan
        DeductionType.DentalInsurance => true,   // §125 cafeteria plan
        DeductionType.VisionInsurance => true,   // §125 cafeteria plan
        DeductionType.HSA             => true,   // §106/125
        DeductionType.FSA             => true,   // §125
        _                             => false,  // after-tax or unknown
    };
}
