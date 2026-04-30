using Finance.Application.Contracts;
using Finance.Application.Engines;
using Finance.Application.Managers.Dependencies;
using Finance.Domain.ValueObjects;

namespace Infrastructure.Engines;

internal sealed class PayrollDeductionEngine : IPayrollDeductionEngine
{
    public NetPayBreakdownResponse ComputeBreakdown(IncomeResponse income, int year, int month)
    {
        var currency = income.Currency;
        var monthlyGross = UserBudgetCalculator.MonthlyEquivalent(income.Amount, income.Frequency);
        var lineItems = new List<DeductionLineItemDto>();

        // ── Pre-tax voluntary deduction total ─────────────────────────────────
        // Traditional 401(k), employer-sponsored health/dental/vision, HSA, and FSA
        // contributions reduce federal and state taxable wages before brackets are applied.
        // We sum these first so the tax functions can subtract them from gross.
        var monthlyPreTax = 0m;
        if (income.Deductions is not null)
        {
            foreach (var d in income.Deductions)
            {
                if (!d.IsTaxExempt && !TaxCalculator.IsPreTaxDeduction(d.Type)) continue;

                if (d.Method == "PercentOfGross")
                    monthlyPreTax += Math.Round(monthlyGross * d.Value / 100m, 2);
                else
                {
                    var freq = Enum.TryParse<RecurrenceFrequency>(d.Frequency, ignoreCase: true, out var f)
                        ? f : RecurrenceFrequency.Monthly;
                    monthlyPreTax += Math.Round(UserBudgetCalculator.MonthlyEquivalent(d.Value, freq), 2);
                }
            }
        }
        var annualPreTax = monthlyPreTax * 12m;

        // ── Engine-computed tax withholding ───────────────────────────────────
        if (income.TaxProfile is not null)
        {
            var annualGross = monthlyGross * 12m;

            var annualFederal = TaxCalculator.ComputeAnnualFederalTax(annualGross, income.TaxProfile, annualPreTax, year);
            lineItems.Add(new DeductionLineItemDto(
                "FederalIncomeTax", "Federal Income Tax", false,
                Math.Round(annualFederal / 12m, 2), currency));

            var annualState = TaxCalculator.ComputeAnnualStateTax(annualGross, income.TaxProfile, annualPreTax, year);
            if (annualState > 0)
            {
                var stateLabel = string.IsNullOrEmpty(income.TaxProfile.StateCode)
                    ? "State Income Tax"
                    : $"State Income Tax ({income.TaxProfile.StateCode})";
                lineItems.Add(new DeductionLineItemDto(
                    "StateIncomeTax", stateLabel, false,
                    Math.Round(annualState / 12m, 2), currency));
            }

            var ss = TaxCalculator.ComputeMonthlySocialSecurity(monthlyGross, year);
            lineItems.Add(new DeductionLineItemDto("SocialSecurity", "Social Security (6.2%)", false, ss, currency));

            var medicare = TaxCalculator.ComputeMonthlyMedicare(monthlyGross);
            lineItems.Add(new DeductionLineItemDto("Medicare", "Medicare (1.45%)", false, medicare, currency));
        }

        // ── Voluntary deductions ──────────────────────────────────────────────
        if (income.Deductions is not null)
        {
            foreach (var d in income.Deductions)
            {
                decimal amount;
                if (d.Method == "PercentOfGross")
                {
                    // Percentage is always per-period and we're computing against monthly gross
                    amount = Math.Round(monthlyGross * d.Value / 100m, 2);
                }
                else
                {
                    // Fixed amount: normalise from the deduction's own frequency to monthly
                    var freq = Enum.TryParse<RecurrenceFrequency>(d.Frequency, ignoreCase: true, out var f)
                        ? f : RecurrenceFrequency.Monthly;
                    amount = Math.Round(UserBudgetCalculator.MonthlyEquivalent(d.Value, freq), 2);
                    amount = Math.Min(amount, monthlyGross);
                }

                lineItems.Add(new DeductionLineItemDto(d.Type, d.Label, d.IsEmployerSponsored, amount, currency));
            }
        }

        var totalDeductions = lineItems.Sum(l => l.Amount);
        var netPay = Math.Max(0m, monthlyGross - totalDeductions);

        return new NetPayBreakdownResponse(
            income.IncomeId,
            Math.Round(monthlyGross, 2),
            currency,
            lineItems.AsReadOnly(),
            Math.Round(totalDeductions, 2),
            Math.Round(netPay, 2));
    }

    public decimal ComputeMonthlyNetPay(IncomeResponse income, int year, int month)
        => ComputeBreakdown(income, year, month).NetPay;
}
