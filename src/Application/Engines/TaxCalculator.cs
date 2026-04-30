using Finance.Application.Contracts;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Engines;

/// <summary>
/// Federal and state income tax withholding estimates.
///
/// TAX YEAR
/// ────────
/// Federal constants and brackets: 2026 (IRS Rev. Proc. 2025-xx, inflation-adjusted).
/// State figures: 2024–2025 per each state's revenue department. States that index
/// their brackets annually may differ slightly; values should be reviewed each year.
///
/// METHODOLOGY
/// ───────────
/// Federal withholding uses the IRS Pub. 15-T "Percentage Method for Automated Payroll":
///   1. Annualise per-period gross:   annualGross = monthlyGross × 12
///   2. Subtract standard deduction and withholding allowances → taxable income
///   3. Apply progressive brackets via <see cref="ApplyBrackets"/>
///   4. Divide annual tax by 12 to get the monthly withholding estimate
///
/// State withholding uses the same annualise → deduct → bracket approach,
/// substituting each state's own standard deduction and rate table.
///
/// KNOWN LIMITATIONS
/// ─────────────────
/// • W-4 Step 3 (dependent/child credits) and Step 4b/4c (extra deductions/withholding)
///   are NOT modelled — these are the primary reason actual withholding may differ from
///   the computed liability estimate.
/// • The Additional Medicare Tax (0.9% above $200 k/yr) is not modelled.
/// • Local/city income taxes (e.g. NYC, Philadelphia) are not modelled.
/// • States marked [APPROX] use only their top marginal flat rate, which overestimates
///   tax for lower-income filers; full bracket tables are not yet implemented for them.
/// </summary>
public static class TaxCalculator
{
    // ── FICA rates (historically stable — no year variance) ─────────────────
    /// <summary>Employee share of Social Security. Employer also pays 6.2%; total OASDI rate is 12.4%.</summary>
    private const decimal SocialSecurityRate = 0.062m;

    /// <summary>
    /// Employee share of Medicare (HI). Employer also pays 1.45%.
    /// Does NOT include the 0.9% Additional Medicare Tax that applies above $200 k/yr.
    /// </summary>
    private const decimal MedicareRate = 0.0145m;

    // ── Year-specific federal rules ───────────────────────────────────────────
    //
    // Federal constants change every year via IRS Rev. Proc. inflation adjustments.
    // Add a new FederalRules entry each October when IRS publishes the following year's Rev. Proc.
    // Years outside the known range clamp to the nearest entry (≤2024 → Rules2024, >2026 → Rules2026).
    //
    // Sources:
    //   2024 — IRS Rev. Proc. 2023-34;  SSA Notice 2023 (wage base $168,600)
    //   2025 — IRS Rev. Proc. 2024-40;  SSA Notice 2024 (wage base $176,100)
    //   2026 — IRS Rev. Proc. 2025-xx;  SSA Notice 2025 [ESTIMATE — official notice pending]

    private sealed record FederalRules(
        decimal AllowanceValue,
        decimal SocialSecurityWageBase,
        decimal SingleStdDed,
        decimal MfjStdDed,
        decimal HohStdDed,
        (decimal Threshold, decimal Rate)[] SingleBrackets,
        (decimal Threshold, decimal Rate)[] MfjBrackets,
        (decimal Threshold, decimal Rate)[] HohBrackets)
    {
        /// <summary>Returns the standard deduction for the given filing status.</summary>
        public decimal StandardDeduction(string filingStatus) => filingStatus switch
        {
            "MarriedFilingJointly" => MfjStdDed,
            "HeadOfHousehold"      => HohStdDed,
            _                      => SingleStdDed,
        };

        /// <summary>Returns the bracket table for the given filing status.</summary>
        public (decimal Threshold, decimal Rate)[] Brackets(string filingStatus) => filingStatus switch
        {
            "MarriedFilingJointly" => MfjBrackets,
            "HeadOfHousehold"      => HohBrackets,
            _                      => SingleBrackets,
        };
    }

    // ── 2024 federal rules — IRS Rev. Proc. 2023-34 ──────────────────────────
    private static readonly FederalRules Rules2024 = new(
        AllowanceValue:          4_300m,
        SocialSecurityWageBase: 168_600m,   // SSA Notice 2023
        SingleStdDed:            14_600m,
        MfjStdDed:               29_200m,
        HohStdDed:               21_900m,
        SingleBrackets:
        [
            (609_350m, 0.37m),   // 37 % on income above $609,350
            (243_725m, 0.35m),   // 35 % on $243,725 – $609,350
            (191_950m, 0.32m),   // 32 % on $191,950 – $243,725
            (100_525m, 0.24m),   // 24 % on $100,525 – $191,950
            ( 47_150m, 0.22m),   // 22 % on $47,150  – $100,525
            ( 11_600m, 0.12m),   // 12 % on $11,600  – $47,150
            (      0m, 0.10m),   // 10 % on $0        – $11,600
        ],
        MfjBrackets:
        [
            (731_200m, 0.37m),   // 37 % on income above $731,200
            (487_450m, 0.35m),   // 35 % on $487,450 – $731,200
            (383_900m, 0.32m),   // 32 % on $383,900 – $487,450
            (201_050m, 0.24m),   // 24 % on $201,050 – $383,900
            ( 94_300m, 0.22m),   // 22 % on $94,300  – $201,050
            ( 23_200m, 0.12m),   // 12 % on $23,200  – $94,300
            (      0m, 0.10m),   // 10 % on $0        – $23,200
        ],
        HohBrackets:
        [
            (609_350m, 0.37m),   // 37 % on income above $609,350
            (243_700m, 0.35m),   // 35 % on $243,700 – $609,350
            (191_950m, 0.32m),   // 32 % on $191,950 – $243,700
            (100_500m, 0.24m),   // 24 % on $100,500 – $191,950
            ( 63_100m, 0.22m),   // 22 % on $63,100  – $100,500
            ( 16_550m, 0.12m),   // 12 % on $16,550  – $63,100
            (      0m, 0.10m),   // 10 % on $0        – $16,550
        ]);

    // ── 2025 federal rules — IRS Rev. Proc. 2024-40 ──────────────────────────
    private static readonly FederalRules Rules2025 = new(
        AllowanceValue:          4_300m,
        SocialSecurityWageBase: 176_100m,   // SSA Notice 2024
        SingleStdDed:            15_000m,
        MfjStdDed:               30_000m,
        HohStdDed:               22_500m,
        SingleBrackets:
        [
            (626_350m, 0.37m),   // 37 % on income above $626,350
            (250_525m, 0.35m),   // 35 % on $250,525 – $626,350
            (197_300m, 0.32m),   // 32 % on $197,300 – $250,525
            (103_350m, 0.24m),   // 24 % on $103,350 – $197,300
            ( 48_475m, 0.22m),   // 22 % on $48,475  – $103,350
            ( 11_925m, 0.12m),   // 12 % on $11,925  – $48,475
            (      0m, 0.10m),   // 10 % on $0        – $11,925
        ],
        MfjBrackets:
        [
            (751_600m, 0.37m),   // 37 % on income above $751,600
            (501_050m, 0.35m),   // 35 % on $501,050 – $751,600
            (394_600m, 0.32m),   // 32 % on $394,600 – $501,050
            (206_700m, 0.24m),   // 24 % on $206,700 – $394,600
            ( 96_950m, 0.22m),   // 22 % on $96,950  – $206,700
            ( 23_850m, 0.12m),   // 12 % on $23,850  – $96,950
            (      0m, 0.10m),   // 10 % on $0        – $23,850
        ],
        HohBrackets:
        [
            (626_350m, 0.37m),   // 37 % on income above $626,350
            (250_500m, 0.35m),   // 35 % on $250,500 – $626,350
            (197_300m, 0.32m),   // 32 % on $197,300 – $250,500
            (103_350m, 0.24m),   // 24 % on $103,350 – $197,300
            ( 64_850m, 0.22m),   // 22 % on $64,850  – $103,350
            ( 17_000m, 0.12m),   // 12 % on $17,000  – $64,850
            (      0m, 0.10m),   // 10 % on $0        – $17,000
        ]);

    // ── 2026 federal rules — IRS Rev. Proc. 2025-xx [ESTIMATE] ───────────────
    // Rev. Proc. for tax year 2026 is typically published Oct/Nov 2025.
    // Amounts below are estimates; update when the official Rev. Proc. is published.
    private static readonly FederalRules Rules2026 = new(
        AllowanceValue:          4_300m,
        SocialSecurityWageBase: 176_100m,   // SSA Notice 2025 [ESTIMATE]
        SingleStdDed:            15_000m,   // [ESTIMATE]
        MfjStdDed:               30_000m,   // [ESTIMATE]
        HohStdDed:               22_500m,   // [ESTIMATE]
        SingleBrackets:
        [
            (626_350m, 0.37m),
            (250_525m, 0.35m),
            (197_300m, 0.32m),
            (103_350m, 0.24m),
            ( 48_475m, 0.22m),
            ( 11_925m, 0.12m),
            (      0m, 0.10m),
        ],
        MfjBrackets:
        [
            (751_600m, 0.37m),
            (501_050m, 0.35m),
            (394_600m, 0.32m),
            (206_700m, 0.24m),
            ( 96_950m, 0.22m),
            ( 23_850m, 0.12m),
            (      0m, 0.10m),
        ],
        HohBrackets:
        [
            (626_350m, 0.37m),
            (250_500m, 0.35m),
            (197_300m, 0.32m),
            (103_350m, 0.24m),
            ( 64_850m, 0.22m),
            ( 17_000m, 0.12m),
            (      0m, 0.10m),
        ]);

    /// <summary>
    /// Returns the federal rules for a given tax year.
    /// Years ≤ 2024 use 2024 rules; years ≥ 2027 use the latest known rules.
    /// Pass <c>0</c> to use the current calendar year.
    /// </summary>
    private static FederalRules GetFederalRules(int year)
    {
        if (year == 0) year = DateTime.UtcNow.Year;
        return year switch
        {
            <= 2024 => Rules2024,
            2025    => Rules2025,
            _       => Rules2026,   // 2026+ — update when new Rev. Proc. is published
        };
    }

    // ── Federal tax computation ───────────────────────────────────────────────

    /// <summary>
    /// Estimates annual federal income tax using the Pub. 15-T Percentage Method.
    ///
    /// Steps:
    ///   1. annualGross − standard deduction − (allowances × allowanceValue) − annualPreTaxDeductions
    ///   2. Apply progressive brackets to the resulting taxable income.
    ///
    /// <paramref name="annualPreTaxDeductions"/> should be the annualised sum of deductions
    /// that reduce W-2 Box 1 taxable wages: Traditional 401(k), employer-sponsored
    /// health/dental/vision premiums (§125), HSA employee contributions, and FSA.
    /// Roth 401(k) and Life Insurance are NOT pre-tax for federal purposes.
    ///
    /// Pass <paramref name="year"/> = 0 (default) to use the current calendar year's rules.
    /// </summary>
    public static decimal ComputeAnnualFederalTax(
        decimal annualGross,
        TaxProfileDto profile,
        decimal annualPreTaxDeductions = 0m,
        int year = 0)
    {
        var rules = GetFederalRules(year);

        var taxableIncome = annualGross
            - rules.StandardDeduction(profile.FilingStatus)
            - profile.FederalAllowances * rules.AllowanceValue
            - annualPreTaxDeductions;   // 401(k) traditional, health/dental/vision §125, HSA, FSA

        if (taxableIncome <= 0) return 0m;

        return ApplyBrackets(taxableIncome, rules.Brackets(profile.FilingStatus));
    }

    // ── FICA ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Employee Social Security (OASDI) withholding for one month.
    /// Wages above the annual wage base are capped; we approximate by capping the monthly amount.
    /// Pass <paramref name="year"/> = 0 (default) to use the current calendar year's wage base.
    /// </summary>
    public static decimal ComputeMonthlySocialSecurity(decimal monthlyGross, int year = 0)
    {
        // Prorate the annual wage base to a monthly cap so high earners aren't double-taxed.
        var monthlyWageBase = GetFederalRules(year).SocialSecurityWageBase / 12m;
        return Math.Round(Math.Min(monthlyGross, monthlyWageBase) * SocialSecurityRate, 2);
    }

    /// <summary>Employee Medicare (HI) withholding for one month. No wage-base cap applies.</summary>
    public static decimal ComputeMonthlyMedicare(decimal monthlyGross)
        => Math.Round(monthlyGross * MedicareRate, 2);

    // ── State tax — entry point ───────────────────────────────────────────────

    /// <summary>States with no individual income tax — always return 0.</summary>
    private static readonly HashSet<string> NoTaxStates =
        ["AK", "FL", "NV", "NH", "SD", "TN", "TX", "WA", "WY"];

    /// <summary>
    /// Estimates annual state income tax.
    ///
    /// Most states conform to federal treatment of pre-tax deductions: Traditional 401(k),
    /// §125 health/dental/vision premiums, HSA, and FSA also reduce state taxable wages.
    /// Notable exceptions (PA, NJ) do NOT allow 401(k) pre-tax treatment — not yet modelled.
    ///
    /// State bracket tables and standard deductions are currently 2024/2025 values.
    /// Year-specific state rule sets will be added as each state publishes annual guidance.
    /// Pass <paramref name="year"/> = 0 (default) to use the current calendar year.
    /// </summary>
    public static decimal ComputeAnnualStateTax(
        decimal annualGross,
        TaxProfileDto profile,
        decimal annualPreTaxDeductions = 0m,
        int year = 0)   // year reserved for future state-rule versioning; not yet used internally
    {
        var stateCode = (profile.StateCode ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(stateCode) || NoTaxStates.Contains(stateCode)) return 0m;

        // Subtract the state standard deduction (returns 0 for states with none or unknown values)
        // plus any state-level withholding allowances the employee has claimed.
        var stateDeduction = StateStandardDeduction(stateCode, profile.FilingStatus)
                           + profile.StateAllowances * GetFederalRules(year).AllowanceValue;

        var taxableIncome = annualGross - stateDeduction - annualPreTaxDeductions;
        if (taxableIncome <= 0) return 0m;

        return StateTax(stateCode, taxableIncome, profile.FilingStatus);
    }

    // ── State standard deductions ─────────────────────────────────────────────
    // Subtracted from annual gross before applying state rates, mirroring the federal approach.
    // States with no standard deduction (NJ, PA, IL, etc.) return 0.
    // "Federal-conforming" states match the 2026 federal amounts ($15,000 / $30,000).
    // All other fixed amounts are 2024 values from each state's revenue department.

    private static decimal StateStandardDeduction(string stateCode, string filingStatus)
    {
        bool mfj = filingStatus == "MarriedFilingJointly";
        return stateCode switch
        {
            // ── Federal-conforming states (2026: $15,000 single / $30,000 MFJ) ──
            "ID" => mfj ? 30_000m : 15_000m,   // ID conforms to federal (ID DOR)
            "AZ" => mfj ? 30_000m : 15_000m,   // AZ conforms to federal (ADOR)
            "CO" => mfj ? 30_000m : 15_000m,   // CO conforms to federal (CO DOR)
            "GA" => mfj ? 30_000m : 15_000m,   // GA conforms to federal (GA DOR)
            "UT" => mfj ? 30_000m : 15_000m,   // UT conforms to federal (USTC)
            "MN" => mfj ? 30_000m : 15_000m,   // MN conforms to federal (MN DOR)
            "MO" => mfj ? 30_000m : 15_000m,   // MO conforms to federal (MO DOR)
            "OR" => mfj ? 30_000m : 15_000m,   // OR conforms to federal (OR DOR)
            "VT" => mfj ? 30_000m : 15_000m,   // VT conforms to federal (VT DOR)
            "DC" => mfj ? 30_000m : 15_000m,   // DC conforms to federal (DC OTR)

            // ── State-specific deductions (2024 values) ──────────────────────
            "AL" => mfj ?  7_500m :  2_500m,   // AL DOR 2024
            "CA" => mfj ? 10_726m :  5_363m,   // CA FTB 2024 Schedule CA (540)
            "DE" =>                  3_250m,    // DE Division of Revenue 2024 (same single/MFJ)
            "HI" => mfj ?  4_400m :  2_200m,   // HI DoTax 2024
            "IA" => mfj ? 14_600m :  7_300m,   // IA DOR 2024 (pre-full-conformity phase-in)
            "KS" => mfj ?  7_500m :  3_000m,   // KS DOR 2024
            "KY" =>                  3_160m,    // KY DOR 2024 (same single/MFJ)
            "MS" => mfj ? 12_000m :  6_000m,   // MS DOR 2024
            "MT" => mfj ?  9_880m :  4_940m,   // MT DOR 2024 [approximate]
            "NC" => mfj ? 21_500m : 10_750m,   // NC DOR 2024
            "NE" => mfj ? 14_600m :  7_300m,   // NE DOR 2024
            "NY" => mfj ? 16_050m :  8_000m,   // NY DTF 2024 Pub. NYS-50-T-NYS
            "OH" => mfj ?  4_800m :  2_400m,   // OH DOR 2024 personal exemption [approximate]
            "RI" => mfj ? 18_800m :  9_400m,   // RI Division of Taxation 2024
            "SC" => mfj ? 14_600m :  7_300m,   // SC DOR 2024 (50 % of prior-year federal)
            "VA" => mfj ? 16_000m :  8_000m,   // VA DOR 2024
            "WI" => mfj ? 21_290m : 11_600m,   // WI DOR 2024 [approximate — sliding scale]

            // ── States with no standard deduction ────────────────────────────
            "CT" => 0m,   // CT has no state standard deduction (CT DRS)
            "IL" => 0m,   // IL uses a personal exemption credit, not a deduction
            "IN" => 0m,   // IN uses a personal exemption, not a deduction
            "MA" => 0m,   // MA has no standard deduction
            "MI" => 0m,   // MI has no standard deduction
            "NJ" => 0m,   // NJ has no standard deduction
            "PA" => 0m,   // PA has no standard deduction

            // All others: return 0 (conservative — prevents over-deducting for unmodelled states)
            _ => 0m,
        };
    }

    // ── State tax rates ───────────────────────────────────────────────────────
    // taxableIncome here is already net of the state standard deduction (see ComputeAnnualStateTax).

    private static decimal StateTax(string stateCode, decimal taxableIncome, string filingStatus)
        => stateCode switch
        {
            // ══ Flat-rate states ══════════════════════════════════════════════
            // A single rate applies to all taxable income. Rates are 2024–2025.

            "AZ" => Flat(taxableIncome, 0.025m),    // 2.5 % flat (AZ DOR 2023+)
            "CO" => Flat(taxableIncome, 0.044m),    // 4.4 % flat (CO DOR 2023+)
            "GA" => Flat(taxableIncome, 0.0549m),   // 5.49 % flat (GA DOR 2024)
            "ID" => Flat(taxableIncome, 0.058m),    // 5.8 % flat (ID STC 2023+)
            "IL" => Flat(taxableIncome, 0.0495m),   // 4.95 % flat (IL DOR)
            "IN" => Flat(taxableIncome, 0.0305m),   // 3.05 % flat (IN DOR 2024)
            "IA" => Flat(taxableIncome, 0.038m),    // 3.8 % flat (IA DOR 2025, phasing down)
            "KY" => Flat(taxableIncome, 0.040m),    // 4.0 % flat (KY DOR 2024)
            "MA" => Flat(taxableIncome, 0.050m),    // 5.0 % flat (MA DOR; +4 % surtax on $1 M+ not modelled)
            "MI" => Flat(taxableIncome, 0.0425m),   // 4.25 % flat (MI Treasury)
            "MS" => Flat(taxableIncome, 0.047m),    // 4.7 % flat (MS DOR 2024)
            "NC" => Flat(taxableIncome, 0.045m),    // 4.5 % flat (NC DOR 2024)
            "PA" => Flat(taxableIncome, 0.0307m),   // 3.07 % flat (PA DOR)
            "UT" => Flat(taxableIncome, 0.0465m),   // 4.65 % flat (USTC 2024)

            // ══ Bracket states ════════════════════════════════════════════════
            // Full progressive bracket tables. See bracket methods below for sources.

            "AL" => ApplyBrackets(taxableIncome, AlBrackets(filingStatus)),
            "CA" => ApplyBrackets(taxableIncome, CaBrackets(filingStatus)),
            "CT" => ApplyBrackets(taxableIncome, CtBrackets(filingStatus)),
            "DC" => ApplyBrackets(taxableIncome, DcBrackets()),
            "DE" => ApplyBrackets(taxableIncome, DeBrackets()),
            "HI" => ApplyBrackets(taxableIncome, HiBrackets()),
            "KS" => ApplyBrackets(taxableIncome, KsBrackets(filingStatus)),
            "MN" => ApplyBrackets(taxableIncome, MnBrackets(filingStatus)),
            "MO" => ApplyBrackets(taxableIncome, MoBrackets()),
            "MT" => ApplyBrackets(taxableIncome, MtBrackets()),
            "NE" => ApplyBrackets(taxableIncome, NeBrackets(filingStatus)),
            "NJ" => ApplyBrackets(taxableIncome, NjBrackets()),
            "NY" => ApplyBrackets(taxableIncome, NyBrackets(filingStatus)),
            "OH" => ApplyBrackets(taxableIncome, OhBrackets()),
            "OR" => ApplyBrackets(taxableIncome, OrBrackets(filingStatus)),
            "RI" => ApplyBrackets(taxableIncome, RiBrackets(filingStatus)),
            "SC" => ApplyBrackets(taxableIncome, ScBrackets()),
            "VA" => ApplyBrackets(taxableIncome, VaBrackets()),
            "VT" => ApplyBrackets(taxableIncome, VtBrackets(filingStatus)),
            "WI" => ApplyBrackets(taxableIncome, WiBrackets(filingStatus)),

            // ══ [APPROX] States — top marginal rate used for all income ═══════
            // These overestimate tax for lower-income filers. Full bracket tables
            // are not yet implemented. Rates are 2024 top marginal rates.

            "AR" => Flat(taxableIncome, 0.039m),    // [APPROX] AR top rate 3.9 % (2024); has 3 brackets
            "LA" => Flat(taxableIncome, 0.0425m),   // [APPROX] LA top rate 4.25 % (2024); has 3 brackets
            "MD" => Flat(taxableIncome, 0.0575m),   // [APPROX] MD top rate 5.75 %; has 8 brackets
            "ME" => Flat(taxableIncome, 0.0715m),   // [APPROX] ME top rate 7.15 %; has 3 brackets
            "NM" => Flat(taxableIncome, 0.059m),    // [APPROX] NM top rate 5.9 %; has 5 brackets
            "OK" => Flat(taxableIncome, 0.0475m),   // [APPROX] OK top rate 4.75 %; has 6 brackets
            "WV" => Flat(taxableIncome, 0.0512m),   // [APPROX] WV top rate 5.12 %; has 5 brackets

            _ => 0m,
        };

    // ── Pre-tax deduction classification ─────────────────────────────────────

    /// <summary>
    /// Returns true for deduction types that reduce federal AND state taxable wages
    /// (W-2 Box 1) before brackets are applied.
    ///
    /// Pre-tax: Traditional 401(k) (§401a), employer-sponsored health/dental/vision
    /// (§125 cafeteria plan), HSA employee contributions (§106/125), FSA (§125).
    ///
    /// NOT pre-tax: Roth 401(k) (after-tax contributions), Life Insurance above the
    /// §79 $50k exclusion, and Other (unknown — treated as taxable to avoid under-withholding).
    /// </summary>
    public static bool IsPreTaxDeduction(string deductionType) =>
        Enum.TryParse<DeductionType>(deductionType, out var t) && t.IsPreTax();

    public static bool IsPreTaxDeduction(DeductionType deductionType) => deductionType.IsPreTax();

    // ── Flat-rate helper ──────────────────────────────────────────────────────

    private static decimal Flat(decimal taxableIncome, decimal rate)
        => Math.Round(taxableIncome * rate, 2);

    // ── State bracket tables ──────────────────────────────────────────────────
    // Format: (lower threshold of this bracket, marginal rate applied to income above it).
    // All tables are ordered highest threshold → lowest so ApplyBrackets works correctly.
    // Sources are cited per table. Thresholds are for the given tax year; verify annually.

    // Alabama (AL) — 3 brackets, 2024
    // Source: Alabama DOR, Form A-4 instructions 2024
    private static (decimal Threshold, decimal Rate)[] AlBrackets(string filingStatus) =>
        filingStatus == "MarriedFilingJointly"
            ? [(6_000m, 0.05m), (1_000m, 0.04m), (0m, 0.02m)]   // 5 % / 4 % / 2 %
            : [(3_000m, 0.05m), (  500m, 0.04m), (0m, 0.02m)];  // thresholds halved for single

    // California (CA) — 9 brackets + 1 % mental health surcharge above $1 M (not modelled)
    // Source: CA FTB 2024 Schedule X (single) / Y (MFJ)
    private static (decimal Threshold, decimal Rate)[] CaBrackets(string filingStatus)
    {
        if (filingStatus == "MarriedFilingJointly")
            return
            [
                (1_443_574m, 0.133m),  // 13.3 % on income above $1,443,574
                (  865_014m, 0.123m),  // 12.3 % on $865,014 – $1,443,574
                (  721_314m, 0.113m),  // 11.3 % on $721,314 – $865,014
                (  111_732m, 0.093m),  //  9.3 % on $111,732 – $721,314
                (   81_212m, 0.080m),  //  8.0 % on $81,212  – $111,732
                (   56_732m, 0.060m),  //  6.0 % on $56,732  – $81,212
                (   30_498m, 0.040m),  //  4.0 % on $30,498  – $56,732
                (   20_498m, 0.020m),  //  2.0 % on $20,498  – $30,498
                (        0m, 0.010m),  //  1.0 % on $0        – $20,498
            ];
        return
        [
            (721_314m, 0.133m),  // 13.3 % on income above $721,314
            (432_787m, 0.123m),  // 12.3 % on $432,787 – $721,314
            (360_659m, 0.113m),  // 11.3 % on $360,659 – $432,787
            ( 70_606m, 0.093m),  //  9.3 % on $70,606  – $360,659
            ( 55_866m, 0.080m),  //  8.0 % on $55,866  – $70,606
            ( 40_245m, 0.060m),  //  6.0 % on $40,245  – $55,866
            ( 25_499m, 0.040m),  //  4.0 % on $25,499  – $40,245
            ( 10_756m, 0.020m),  //  2.0 % on $10,756  – $25,499
            (      0m, 0.010m),  //  1.0 % on $0        – $10,756
        ];
    }

    // Connecticut (CT) — 7 brackets, 2024
    // Source: CT DRS Publication IP-2024(7)
    private static (decimal Threshold, decimal Rate)[] CtBrackets(string filingStatus)
    {
        if (filingStatus == "MarriedFilingJointly")
            return
            [
                (1_000_000m, 0.0699m),  // 6.99 % on income above $1,000,000
                (  500_000m, 0.069m),   // 6.9 %  on $500,000  – $1,000,000
                (  400_000m, 0.065m),   // 6.5 %  on $400,000  – $500,000
                (  200_000m, 0.060m),   // 6.0 %  on $200,000  – $400,000
                (  100_000m, 0.055m),   // 5.5 %  on $100,000  – $200,000
                (   20_000m, 0.050m),   // 5.0 %  on $20,000   – $100,000
                (        0m, 0.030m),   // 3.0 %  on $0         – $20,000
            ];
        return
        [
            (500_000m, 0.0699m),  // 6.99 % on income above $500,000
            (250_000m, 0.069m),   // 6.9 %  on $250,000 – $500,000
            (200_000m, 0.065m),   // 6.5 %  on $200,000 – $250,000
            (100_000m, 0.060m),   // 6.0 %  on $100,000 – $200,000
            ( 50_000m, 0.055m),   // 5.5 %  on $50,000  – $100,000
            ( 10_000m, 0.050m),   // 5.0 %  on $10,000  – $50,000
            (      0m, 0.030m),   // 3.0 %  on $0        – $10,000
        ];
    }

    // Washington DC — 7 brackets, 2024
    // Source: DC OTR Tax Year 2024 withholding tables
    private static (decimal Threshold, decimal Rate)[] DcBrackets() =>
    [
        (1_000_000m, 0.1075m),  // 10.75 % on income above $1,000,000
        (  500_000m, 0.0975m),  //  9.75 % on $500,000  – $1,000,000
        (  250_000m, 0.0850m),  //  8.5 %  on $250,000  – $500,000
        (   60_000m, 0.0650m),  //  6.5 %  on $60,000   – $250,000
        (   40_000m, 0.0600m),  //  6.0 %  on $40,000   – $60,000
        (   10_000m, 0.0400m),  //  4.0 %  on $10,000   – $40,000
        (        0m, 0.0400m),  //  4.0 %  on $0         – $10,000
    ];

    // Delaware (DE) — 7 brackets, 2024 (same for all filing statuses)
    // Source: DE Division of Revenue 2024 Instructions
    private static (decimal Threshold, decimal Rate)[] DeBrackets() =>
    [
        (60_000m, 0.066m),   // 6.6 %  on income above $60,000
        (25_000m, 0.0555m),  // 5.55 % on $25,000 – $60,000
        (20_000m, 0.052m),   // 5.2 %  on $20,000 – $25,000
        (10_000m, 0.048m),   // 4.8 %  on $10,000 – $20,000
        ( 5_000m, 0.039m),   // 3.9 %  on $5,000  – $10,000
        ( 2_000m, 0.022m),   // 2.2 %  on $2,000  – $5,000
        (     0m, 0.000m),   // 0 %    on $0       – $2,000
    ];

    // Hawaii (HI) — 9 brackets, 2024
    // Source: HI DoTax Form HW-4 and Rate Schedules 2024
    private static (decimal Threshold, decimal Rate)[] HiBrackets() =>
    [
        (200_000m, 0.110m),  // 11.0 % on income above $200,000
        (100_000m, 0.090m),  //  9.0 % on $100,000 – $200,000
        ( 48_000m, 0.079m),  //  7.9 % on $48,000  – $100,000
        ( 36_000m, 0.068m),  //  6.8 % on $36,000  – $48,000
        ( 24_000m, 0.064m),  //  6.4 % on $24,000  – $36,000
        ( 12_000m, 0.055m),  //  5.5 % on $12,000  – $24,000
        (  4_800m, 0.034m),  //  3.4 % on $4,800   – $12,000
        (  2_400m, 0.032m),  //  3.2 % on $2,400   – $4,800
        (      0m, 0.014m),  //  1.4 % on $0        – $2,400
    ];

    // Kansas (KS) — 2 brackets, 2024
    // Source: KS DOR Publication KW-100 2024
    // MFJ thresholds are double the single thresholds.
    private static (decimal Threshold, decimal Rate)[] KsBrackets(string filingStatus)
    {
        decimal lower = filingStatus == "MarriedFilingJointly" ? 30_000m : 15_000m;
        return
        [
            (lower, 0.057m),  // 5.7 % on income above threshold
            (   0m, 0.031m),  // 3.1 % on $0 – threshold
        ];
    }

    // Minnesota (MN) — 4 brackets, 2024
    // Source: MN DOR Withholding Tax Tables 2024
    // FIXED: second threshold corrected from $87,110 to $98,760 (prior value was wrong).
    private static (decimal Threshold, decimal Rate)[] MnBrackets(string filingStatus)
    {
        if (filingStatus == "MarriedFilingJointly")
            return
            [
                (366_680m, 0.0985m),  // 9.85 % on income above $366,680
                (197_570m, 0.0785m),  // 7.85 % on $197,570 – $366,680
                ( 60_150m, 0.0680m),  // 6.80 % on $60,150  – $197,570
                (      0m, 0.0535m),  // 5.35 % on $0        – $60,150
            ];
        return
        [
            (183_340m, 0.0985m),  // 9.85 % on income above $183,340
            ( 98_760m, 0.0785m),  // 7.85 % on $98,760  – $183,340
            ( 30_070m, 0.0680m),  // 6.80 % on $30,070  – $98,760
            (      0m, 0.0535m),  // 5.35 % on $0        – $30,070
        ];
    }

    // Missouri (MO) — 9 brackets (many small slices below $10k), top rate 4.8 % as of 2024
    // Source: MO DOR Form MO W-4 Withholding Instructions 2024
    private static (decimal Threshold, decimal Rate)[] MoBrackets() =>
    [
        ( 9_656m, 0.048m),  // 4.8 % on income above $9,656
        ( 8_449m, 0.045m),  // 4.5 % on $8,449 – $9,656
        ( 7_242m, 0.040m),  // 4.0 % on $7,242 – $8,449
        ( 6_035m, 0.035m),  // 3.5 % on $6,035 – $7,242
        ( 4_828m, 0.030m),  // 3.0 % on $4,828 – $6,035
        ( 3_621m, 0.025m),  // 2.5 % on $3,621 – $4,828
        ( 2_414m, 0.020m),  // 2.0 % on $2,414 – $3,621
        ( 1_207m, 0.015m),  // 1.5 % on $1,207 – $2,414
        (     0m, 0.000m),  // 0 %   on $0      – $1,207
    ];

    // Montana (MT) — 2 brackets (simplified 2024+)
    // Source: MT DOR 2024 Withholding Tax Guide
    private static (decimal Threshold, decimal Rate)[] MtBrackets() =>
    [
        (20_500m, 0.059m),  // 5.9 % on income above $20,500
        (     0m, 0.047m),  // 4.7 % on $0 – $20,500
    ];

    // Nebraska (NE) — 4 brackets, 2024
    // Source: NE DOR Nebraska Circular EN 2024
    private static (decimal Threshold, decimal Rate)[] NeBrackets(string filingStatus)
    {
        if (filingStatus == "MarriedFilingJointly")
            return
            [
                (71_460m, 0.0664m),  // 6.64 % on income above $71,460
                (44_350m, 0.0501m),  // 5.01 % on $44,350 – $71,460
                ( 7_390m, 0.0351m),  // 3.51 % on $7,390  – $44,350
                (     0m, 0.0246m),  // 2.46 % on $0       – $7,390
            ];
        return
        [
            (35_730m, 0.0664m),  // 6.64 % on income above $35,730
            (22_170m, 0.0501m),  // 5.01 % on $22,170 – $35,730
            ( 3_700m, 0.0351m),  // 3.51 % on $3,700  – $22,170
            (     0m, 0.0246m),  // 2.46 % on $0       – $3,700
        ];
    }

    // New Jersey (NJ) — 7 brackets, 2024
    // Source: NJ Division of Taxation NJ-WT 2024
    // FIXED: prior version had brackets out of order (thresholds 35k→40k→20k — invalid for ApplyBrackets)
    // and used incorrect rates. This table is correct highest → lowest.
    private static (decimal Threshold, decimal Rate)[] NjBrackets() =>
    [
        (1_000_000m, 0.1075m),  // 10.75 % on income above $1,000,000
        (  500_000m, 0.0897m),  //  8.97 % on $500,000 – $1,000,000
        (   75_000m, 0.0637m),  //  6.37 % on $75,000  – $500,000
        (   40_000m, 0.0535m),  //  5.35 % on $40,000  – $75,000
        (   35_000m, 0.035m),   //  3.5 %  on $35,000  – $40,000
        (   20_000m, 0.0175m),  //  1.75 % on $20,000  – $35,000
        (        0m, 0.014m),   //  1.4 %  on $0        – $20,000
    ];

    // New York (NY) — 9 brackets, 2024
    // Source: NY DTF Publication NYS-50-T-NYS 2024
    private static (decimal Threshold, decimal Rate)[] NyBrackets(string filingStatus)
    {
        if (filingStatus == "MarriedFilingJointly")
            return
            [
                (25_000_000m, 0.109m),   // 10.9 %  on income above $25,000,000
                ( 5_000_000m, 0.103m),   // 10.3 %  on $5,000,000  – $25,000,000
                ( 2_155_350m, 0.0965m),  //  9.65 % on $2,155,350  – $5,000,000
                (   323_200m, 0.0685m),  //  6.85 % on $323,200    – $2,155,350
                (   161_550m, 0.0600m),  //  6.0 %  on $161,550    – $323,200
                (    40_000m, 0.0550m),  //  5.5 %  on $40,000     – $161,550
                (    27_900m, 0.0525m),  //  5.25 % on $27,900     – $40,000
                (    17_150m, 0.0450m),  //  4.5 %  on $17,150     – $27,900
                (         0m, 0.040m),   //  4.0 %  on $0           – $17,150
            ];
        return
        [
            (25_000_000m, 0.109m),   // 10.9 %  on income above $25,000,000
            ( 5_000_000m, 0.103m),   // 10.3 %  on $5,000,000  – $25,000,000
            ( 2_155_350m, 0.0965m),  //  9.65 % on $2,155,350  – $5,000,000
            (   323_200m, 0.0685m),  //  6.85 % on $323,200    – $2,155,350
            (   161_550m, 0.0600m),  //  6.0 %  on $161,550    – $323,200
            (    27_900m, 0.0550m),  //  5.5 %  on $27,900     – $161,550
            (    23_600m, 0.0525m),  //  5.25 % on $23,600     – $27,900
            (    17_150m, 0.0450m),  //  4.5 %  on $17,150     – $23,600
            (         0m, 0.040m),   //  4.0 %  on $0           – $17,150
        ];
    }

    // Ohio (OH) — 5 brackets (simplified post-2023 tax reform)
    // Source: OH Department of Taxation 2024 Employer Withholding Tables
    private static (decimal Threshold, decimal Rate)[] OhBrackets() =>
    [
        (115_300m, 0.0399m),  // 3.99 % on income above $115,300
        ( 92_150m, 0.0369m),  // 3.69 % on $92,150  – $115,300
        ( 46_100m, 0.0323m),  // 3.23 % on $46,100  – $92,150
        ( 26_050m, 0.0277m),  // 2.77 % on $26,050  – $46,100
        (      0m, 0.000m),   // 0 %   on $0         – $26,050
    ];

    // Oregon (OR) — 4 brackets, 2024
    // Source: OR DOR Publication OR-WITHHOLDING 2024
    // FIXED: prior thresholds ($8,750 / $17,400 / $250,000) were wrong.
    // Correct single thresholds: $10,200 / $25,500 / $125,000.
    private static (decimal Threshold, decimal Rate)[] OrBrackets(string filingStatus)
    {
        if (filingStatus == "MarriedFilingJointly")
            return
            [
                (250_000m, 0.099m),   // 9.9 %  on income above $250,000
                ( 51_000m, 0.0875m),  // 8.75 % on $51,000 – $250,000
                ( 20_400m, 0.0675m),  // 6.75 % on $20,400 – $51,000
                (      0m, 0.0475m),  // 4.75 % on $0       – $20,400
            ];
        return
        [
            (125_000m, 0.099m),   // 9.9 %  on income above $125,000
            ( 25_500m, 0.0875m),  // 8.75 % on $25,500 – $125,000
            ( 10_200m, 0.0675m),  // 6.75 % on $10,200 – $25,500
            (      0m, 0.0475m),  // 4.75 % on $0       – $10,200
        ];
    }

    // Rhode Island (RI) — 3 brackets, 2024
    // Source: RI Division of Taxation Withholding Tax Tables 2024
    private static (decimal Threshold, decimal Rate)[] RiBrackets(string filingStatus)
    {
        if (filingStatus == "MarriedFilingJointly")
            return
            [
                (333_900m, 0.0599m),  // 5.99 % on income above $333,900
                (146_900m, 0.0475m),  // 4.75 % on $146,900 – $333,900
                (      0m, 0.0375m),  // 3.75 % on $0        – $146,900
            ];
        return
        [
            (166_950m, 0.0599m),  // 5.99 % on income above $166,950
            ( 73_450m, 0.0475m),  // 4.75 % on $73,450 – $166,950
            (      0m, 0.0375m),  // 3.75 % on $0       – $73,450
        ];
    }

    // South Carolina (SC) — 3 brackets (simplified 2022+), 2024
    // Source: SC DOR Form SC W-4 Instructions 2024
    private static (decimal Threshold, decimal Rate)[] ScBrackets() =>
    [
        (16_040m, 0.064m),  // 6.4 % on income above $16,040
        ( 3_200m, 0.030m),  // 3.0 % on $3,200 – $16,040
        (     0m, 0.000m),  // 0 %   on $0      – $3,200
    ];

    // Virginia (VA) — 4 brackets (unchanged for many years), 2024
    // Source: VA Department of Taxation Form VA-4 Instructions 2024
    private static (decimal Threshold, decimal Rate)[] VaBrackets() =>
    [
        (17_000m, 0.0575m),  // 5.75 % on income above $17,000
        ( 5_000m, 0.050m),   // 5.0 %  on $5,000  – $17,000
        ( 3_000m, 0.030m),   // 3.0 %  on $3,000  – $5,000
        (     0m, 0.020m),   // 2.0 %  on $0       – $3,000
    ];

    // Vermont (VT) — 4 brackets, 2024
    // Source: VT Department of Taxes Withholding Tables 2024
    private static (decimal Threshold, decimal Rate)[] VtBrackets(string filingStatus)
    {
        if (filingStatus == "MarriedFilingJointly")
            return
            [
                (279_450m, 0.0875m),  // 8.75 % on income above $279,450
                (183_400m, 0.076m),   // 7.6 %  on $183,400 – $279,450
                ( 75_850m, 0.066m),   // 6.6 %  on $75,850  – $183,400
                (      0m, 0.0335m),  // 3.35 % on $0        – $75,850
            ];
        return
        [
            (229_550m, 0.0875m),  // 8.75 % on income above $229,550
            (110_050m, 0.076m),   // 7.6 %  on $110,050 – $229,550
            ( 45_400m, 0.066m),   // 6.6 %  on $45,400  – $110,050
            (      0m, 0.0335m),  // 3.35 % on $0        – $45,400
        ];
    }

    // Wisconsin (WI) — 4 brackets, 2024
    // Source: WI DOR Publication W-166 2024
    private static (decimal Threshold, decimal Rate)[] WiBrackets(string filingStatus)
    {
        if (filingStatus == "MarriedFilingJointly")
            return
            [
                (420_420m, 0.0765m),  // 7.65 % on income above $420,420
                ( 38_190m, 0.053m),   // 5.3 %  on $38,190 – $420,420
                ( 19_090m, 0.044m),   // 4.4 %  on $19,090 – $38,190
                (      0m, 0.035m),   // 3.5 %  on $0       – $19,090
            ];
        return
        [
            (315_310m, 0.0765m),  // 7.65 % on income above $315,310
            ( 28_640m, 0.053m),   // 5.3 %  on $28,640 – $315,310
            ( 14_320m, 0.044m),   // 4.4 %  on $14,320 – $28,640
            (      0m, 0.035m),   // 3.5 %  on $0       – $14,320
        ];
    }

    // ── Core bracket engine ───────────────────────────────────────────────────

    /// <summary>
    /// Applies a progressive bracket table to taxable income and returns the total tax.
    ///
    /// Algorithm: iterate from the highest bracket down. For each bracket, compute tax
    /// on the slice of income above the bracket's lower threshold, then reduce the
    /// running income to that threshold so lower brackets only see the remaining slice.
    ///
    /// Example for Single 2026 on $60,000 taxable income:
    ///   $60,000 > $48,475 → tax += ($60,000 - $48,475) × 22 % = $2,535.50; income → $48,475
    ///   $48,475 > $11,925 → tax += ($48,475 - $11,925) × 12 % = $4,386.00; income → $11,925
    ///   $11,925 > $0      → tax += ($11,925 - $0)      × 10 % = $1,192.50; income → $0
    ///   Total = $8,114.00
    ///
    /// Brackets MUST be ordered highest threshold first or the result will be wrong.
    /// </summary>
    private static decimal ApplyBrackets(decimal taxableIncome, (decimal Threshold, decimal Rate)[] brackets)
    {
        decimal tax = 0m;
        foreach (var (threshold, rate) in brackets)
        {
            if (taxableIncome > threshold)
            {
                // Tax only the slice above this threshold; lower brackets will handle the rest.
                tax += (taxableIncome - threshold) * rate;
                taxableIncome = threshold;
            }
        }
        return Math.Round(tax, 2);
    }
}
