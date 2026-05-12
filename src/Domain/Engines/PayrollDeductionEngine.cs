using Finance.Domain.Utilities;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Engines;

/// <summary>
/// Computes monthly net pay breakdown for an income source applying
/// tax withholding and voluntary deductions.
/// Change driver: tax calculation rules and deduction ordering logic.
/// </summary>
public static class PayrollDeductionEngine
{
    public static NetPayBreakdown ComputeBreakdown(
        Guid incomeId,
        decimal grossAmount,
        RecurrenceFrequency frequency,
        string currency,
        TaxWithholdingProfile? taxProfile,
        IReadOnlyList<PayrollDeduction>? deductions,
        int year,
        int month)
    {
        var monthlyGross = UserBudgetCalculator.MonthlyEquivalent(grossAmount, frequency);
        var lineItems = new List<DeductionLineItem>();

        // ── Pre-tax voluntary deduction total ────────────────────────────────
        var monthlyPreTax = 0m;
        if (deductions is not null)
        {
            foreach (var d in deductions)
            {
                if (!d.IsTaxExempt && !TaxCalculator.IsPreTaxDeduction(d.Type)) continue;

                if (d.Method == DeductionCalculationMethod.PercentOfGross)
                    monthlyPreTax += Math.Round(monthlyGross * d.Value / 100m, 2);
                else
                    monthlyPreTax += Math.Round(UserBudgetCalculator.MonthlyEquivalent(d.Value, d.Frequency), 2);
            }
        }
        monthlyPreTax = Math.Min(monthlyPreTax, monthlyGross);
        var annualPreTax = monthlyPreTax * 12m;

        // ── Engine-computed tax withholding ───────────────────────────────────
        if (taxProfile is not null)
        {
            var annualGross = monthlyGross * 12m;

            var annualFederal = TaxCalculator.ComputeAnnualFederalTax(annualGross, taxProfile, annualPreTax, year);
            lineItems.Add(new DeductionLineItem(
                "FederalIncomeTax", "Federal Income Tax", false,
                Math.Round(annualFederal / 12m, 2), currency));

            var annualState = TaxCalculator.ComputeAnnualStateTax(annualGross, taxProfile, annualPreTax, year);
            if (annualState > 0)
            {
                var stateLabel = string.IsNullOrEmpty(taxProfile.StateCode)
                    ? "State Income Tax"
                    : $"State Income Tax ({taxProfile.StateCode})";
                lineItems.Add(new DeductionLineItem(
                    "StateIncomeTax", stateLabel, false,
                    Math.Round(annualState / 12m, 2), currency));
            }

            var ss = TaxCalculator.ComputeMonthlySocialSecurity(monthlyGross, year);
            lineItems.Add(new DeductionLineItem("SocialSecurity", "Social Security (6.2%)", false, ss, currency));

            var medicare = TaxCalculator.ComputeMonthlyMedicare(monthlyGross);
            lineItems.Add(new DeductionLineItem("Medicare", "Medicare (1.45%)", false, medicare, currency));
        }

        // ── Voluntary deductions ──────────────────────────────────────────────
        var taxLineSum = lineItems.Sum(l => l.Amount);
        var voluntaryRemaining = Math.Max(0m, monthlyGross - taxLineSum);
        var voluntaryRunning = 0m;
        if (deductions is not null)
        {
            foreach (var d in deductions)
            {
                decimal raw = d.Method == DeductionCalculationMethod.PercentOfGross
                    ? Math.Round(monthlyGross * d.Value / 100m, 2)
                    : Math.Round(UserBudgetCalculator.MonthlyEquivalent(d.Value, d.Frequency), 2);

                var amount = Math.Min(raw, Math.Max(0m, voluntaryRemaining - voluntaryRunning));
                voluntaryRunning += amount;

                lineItems.Add(new DeductionLineItem(d.Type.ToString(), d.Label, d.IsEmployerSponsored, amount, currency));
            }
        }

        var totalDeductions = lineItems.Sum(l => l.Amount);
        var netPay = Math.Max(0m, monthlyGross - totalDeductions);

        return new NetPayBreakdown(
            incomeId,
            Math.Round(monthlyGross, 2),
            currency,
            lineItems.AsReadOnly(),
            Math.Round(totalDeductions, 2),
            Math.Round(netPay, 2));
    }

    public static decimal ComputeMonthlyNetPay(
        decimal grossAmount,
        RecurrenceFrequency frequency,
        TaxWithholdingProfile? taxProfile,
        IReadOnlyList<PayrollDeduction>? deductions,
        int year,
        int month)
        => ComputeBreakdown(Guid.Empty, grossAmount, frequency, string.Empty, taxProfile, deductions, year, month).NetPay;
}
