using Finance.Application.Contracts;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Engines;

/// <summary>
/// Shared helpers for computing a user's personal budget figures (income and obligations).
/// Used by both the overview endpoint and the per-household detail endpoint so "Net Balance"
/// is consistent everywhere: a user's own monthly income minus the sum of their split
/// obligations across ALL households.
/// </summary>
public static class UserBudgetCalculator
{
    /// <summary>
    /// Returns the monthly-equivalent amount of an income source, normalised so Weekly/BiWeekly/Quarterly/etc.
    /// all map onto a per-month figure suitable for budgeting.
    /// </summary>
    public static decimal MonthlyEquivalent(IncomeResponse src) =>
        MonthlyEquivalent(src.Amount, src.Frequency);

    /// <summary>
    /// Returns the monthly-equivalent of a raw amount + frequency combination.
    /// This overload is the canonical computation; the <see cref="MonthlyEquivalent(IncomeResponse)"/>
    /// overload delegates here so the switch table is defined exactly once.
    /// </summary>
    public static decimal MonthlyEquivalent(decimal amount, RecurrenceFrequency frequency) =>
        amount * frequency.ToMonthlyFactor();

    /// <summary>
    /// Sum of all active income sources the user has, normalised to a monthly figure.
    /// Only sources that are active for the given (year, month) are included.
    /// </summary>
    public static decimal MonthlyIncomeForUser(IEnumerable<IncomeResponse> incomeSources, int year, int month)
    {
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        decimal total = 0m;
        foreach (var src in incomeSources)
        {
            if (!src.IsActive) continue;
            if (src.StartDate > monthEnd) continue;
            if (src.EndDate.HasValue && src.EndDate.Value < monthStart) continue;
            total += MonthlyEquivalent(src);
        }
        return total;
    }

    /// <summary>
    /// Returns the monthly-equivalent net take-home pay for an income source after all payroll
    /// deductions (tax withholding + voluntary). Falls back to gross when no deductions are configured.
    /// </summary>
    public static decimal MonthlyNetEquivalent(IncomeResponse src, int year = 0)
    {
        var monthlyGross = MonthlyEquivalent(src);

        var hasTax = src.TaxProfile is not null;
        var hasDeductions = src.Deductions is { Count: > 0 };
        if (!hasTax && !hasDeductions) return monthlyGross;

        var annualGross = monthlyGross * 12m;
        var totalMonthlyDeductions = 0m;

        // Sum pre-tax deductions first — they reduce taxable income before brackets are applied.
        var monthlyPreTax = 0m;
        if (src.Deductions is not null)
        {
            foreach (var d in src.Deductions)
            {
                if (!d.IsTaxExempt && !TaxCalculator.IsPreTaxDeduction(d.Type)) continue;
                if (d.Method == "PercentOfGross")
                    monthlyPreTax += Math.Round(monthlyGross * d.Value / 100m, 2);
                else
                {
                    var freq = Enum.TryParse<RecurrenceFrequency>(d.Frequency, ignoreCase: true, out var f)
                        ? f : RecurrenceFrequency.Monthly;
                    monthlyPreTax += Math.Round(MonthlyEquivalent(d.Value, freq), 2);
                }
            }
        }
        var annualPreTax = monthlyPreTax * 12m;

        if (src.TaxProfile is not null)
        {
            totalMonthlyDeductions += TaxCalculator.ComputeAnnualFederalTax(annualGross, src.TaxProfile, annualPreTax, year) / 12m;
            totalMonthlyDeductions += TaxCalculator.ComputeAnnualStateTax(annualGross, src.TaxProfile, annualPreTax, year) / 12m;
            totalMonthlyDeductions += TaxCalculator.ComputeMonthlySocialSecurity(monthlyGross, year);
            totalMonthlyDeductions += TaxCalculator.ComputeMonthlyMedicare(monthlyGross);
        }

        if (src.Deductions is not null)
        {
            foreach (var d in src.Deductions)
            {
                if (d.Method == "PercentOfGross")
                {
                    totalMonthlyDeductions += Math.Round(monthlyGross * d.Value / 100m, 2);
                }
                else
                {
                    var freq = Enum.TryParse<RecurrenceFrequency>(d.Frequency, ignoreCase: true, out var f)
                        ? f : RecurrenceFrequency.Monthly;
                    totalMonthlyDeductions += Math.Min(
                        Math.Round(MonthlyEquivalent(d.Value, freq), 2),
                        monthlyGross);
                }
            }
        }

        return Math.Max(0m, monthlyGross - totalMonthlyDeductions);
    }

    /// <summary>
    /// Sum of monthly NET income for all active sources in the given month.
    /// Net income = gross minus estimated payroll deductions (tax + voluntary).
    /// </summary>
    public static decimal MonthlyNetIncomeForUser(IEnumerable<IncomeResponse> incomeSources, int year, int month)
    {
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        decimal total = 0m;
        foreach (var src in incomeSources)
        {
            if (!src.IsActive) continue;
            if (src.StartDate > monthEnd) continue;
            if (src.EndDate.HasValue && src.EndDate.Value < monthStart) continue;
            total += MonthlyNetEquivalent(src);
        }
        return total;
    }
    /// Recurring bills are projected into the month via <see cref="RecurrenceSchedule.GetOccurrencesInRange"/>;
    /// one-time bills are counted if their DueDate falls inside the month.
    /// </summary>
    public static decimal MonthlyObligationsForUser(IEnumerable<SplitWithBillDetail> splits, int year, int month)
    {
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEndExclusive = monthStart.AddMonths(1);

        decimal total = 0m;
        foreach (var s in splits)
        {
            int occurrences;
            if (s.RecurrenceFrequency.HasValue && s.RecurrenceStartDate.HasValue)
            {
                var schedule = RecurrenceSchedule.Create(
                    s.RecurrenceFrequency.Value,
                    s.RecurrenceStartDate.Value,
                    s.RecurrenceEndDate);
                occurrences = schedule.GetOccurrencesInRange(monthStart, monthEndExclusive).Count;
            }
            else
            {
                occurrences = (s.DueDate >= monthStart && s.DueDate < monthEndExclusive) ? 1 : 0;
            }
            total += occurrences * s.Amount;
        }
        return total;
    }
}
