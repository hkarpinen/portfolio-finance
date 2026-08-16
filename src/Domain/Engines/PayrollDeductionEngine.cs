using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Engines;

/// <summary>
/// Computes monthly net pay breakdown for an income source applying
/// tax withholding and voluntary deductions.
/// Change driver: tax calculation rules and deduction ordering logic.
/// </summary>
public interface IPayrollDeductionEngine
{
    /// <summary>
    /// What is left of one month of this income after withholding and voluntary deductions.
    /// Takes the income itself, so "this income's monthly gross" has one derivation.
    /// </summary>
    NetPayBreakdown ComputeBreakdown(IncomeSource income, int year, int month);

    decimal ComputeMonthlyNetPay(IncomeSource income, int year, int month);
}

internal sealed class PayrollDeductionEngine : IPayrollDeductionEngine
{
    public NetPayBreakdown ComputeBreakdown(IncomeSource income, int year, int month)
    {
        var incomeId = income.Id.Value;
        var currency = income.Amount.Currency;
        var taxProfile = income.TaxProfile;
        var deductions = income.Deductions.Count > 0 ? income.Deductions : null;
        var monthlyGross = income.MonthlyGross();
        var lineItems = new List<DeductionLineItem>();

        var monthlyPreTax = 0m;
        if (deductions is not null)
        {
            foreach (var d in deductions)
            {
                if (!d.ReducesTaxableIncome) continue;

                monthlyPreTax += d.MonthlyAmount(monthlyGross);
            }
        }
        monthlyPreTax = Math.Min(monthlyPreTax, monthlyGross);
        var annualPreTax = monthlyPreTax * 12m;

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

        var taxLineSum = lineItems.Sum(l => l.Amount);
        var voluntaryRemaining = Math.Max(0m, monthlyGross - taxLineSum);
        var voluntaryRunning = 0m;
        if (deductions is not null)
        {
            foreach (var d in deductions)
            {
                var raw = d.MonthlyAmount(monthlyGross);
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

    public decimal ComputeMonthlyNetPay(IncomeSource income, int year, int month)
        => ComputeBreakdown(income, year, month).NetPay;
}
