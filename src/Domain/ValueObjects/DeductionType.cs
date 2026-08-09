namespace Finance.Domain.ValueObjects;

// Tax types (Federal/State/FICA) are never stored — the payroll engine computes them from the
// income source's withholding profile. Voluntary types ARE stored on the aggregate and computed
// from their declared method/value.
public enum DeductionType
{
    // IsPreTax() below is the single source of truth for pre-tax classification.
    FederalIncomeTax,
    StateIncomeTax,
    SocialSecurity,
    Medicare,

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
    // True when the type reduces federal/state taxable wages (W-2 Box 1) before income-tax brackets
    // are applied — §125 cafeteria plan, §401(a), §106/125 HSA/FSA.
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
