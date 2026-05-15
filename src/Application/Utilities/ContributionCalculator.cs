using Finance.Application.Dtos;
using Finance.Domain.Aggregates;
using Finance.Domain.Engines;
using Finance.Domain.Utilities;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Utilities;

/// <summary>
/// Assembles per-month contribution summaries from pre-fetched domain entities
/// and payment-occurrence dictionaries.
///
/// Computation (recurrence scheduling, paycheck counting, net-pay math) is
/// delegated to <see cref="IncomeSource"/> entity methods and
/// <see cref="IPayrollDeductionEngine"/> — no DTO unpacking occurs here.
/// Callers are responsible for fetching domain entities; this class maps the
/// computed results to response DTOs at the output boundary.
/// </summary>
public static class ContributionCalculator
{
    public static IReadOnlyCollection<ContributionPeriodSummaryDto> BuildSummaries(
        DateTime now,
        int monthCount,
        int pastMonths,
        IReadOnlyList<IncomeSource> incomeSources,
        IReadOnlyList<Expense> personalExpenses,
        IReadOnlyList<(ExpenseSplit Split, Expense Expense)> splits,
        IReadOnlyDictionary<(Guid SplitId, DateTime OccurrenceDate), DateTime> paidSplitOccurrences,
        IReadOnlyDictionary<(Guid ExpenseId, DateTime OccurrenceDate), DateTime> paidPersonalBillOccurrences)
    {
        var windowStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-pastMonths);
        var windowEndExclusive = windowStart.AddMonths(monthCount);

        var activeSources = incomeSources.Where(s => s.IsActive).ToList();
        var activePersonal = personalExpenses.Where(e => e.IsActive).ToList();

        // ── Project split occurrences across the window ──────────────────────

        var projected = new List<(DateTime OccurrenceDate, ContributionItemDto Item)>();
        foreach (var s in splits)
        {
            IEnumerable<DateTime> occurrenceDates = s.Expense.RecurrenceSchedule is not null
                ? s.Expense.RecurrenceSchedule.GetOccurrencesInRange(windowStart, windowEndExclusive)
                : (IEnumerable<DateTime>)[s.Expense.DueDate];

            foreach (var date in occurrenceDates)
            {
                var isClaimed = paidSplitOccurrences.TryGetValue((s.Split.Id.Value, date.Date), out var claimedAt);
                projected.Add((date, new ContributionItemDto(
                    s.Split.Id.Value, s.Expense.Id.Value, s.Expense.Title, s.Expense.Category.ToString(),
                    s.Split.Amount.Amount, s.Split.Amount.Currency, date,
                    isClaimed,
                    s.Split.GroupId.Value,
                    isClaimed ? claimedAt : null)));
            }
        }

        // ── Project personal expense occurrences across the window ───────────

        var projectedPersonal = new List<(DateTime OccurrenceDate, PersonalBillItemDto Item)>();
        foreach (var e in activePersonal)
        {
            IEnumerable<DateTime> occurrenceDates = e.RecurrenceSchedule is not null
                ? e.RecurrenceSchedule.GetOccurrencesInRange(windowStart, windowEndExclusive)
                : (IEnumerable<DateTime>)[e.DueDate];

            foreach (var date in occurrenceDates)
            {
                var isPaid = paidPersonalBillOccurrences.ContainsKey((e.Id.Value, date.Date));
                projectedPersonal.Add((date, new PersonalBillItemDto(
                    e.Id.Value, e.Title, e.Category.ToString(),
                    e.Amount.Amount, e.Amount.Currency, date, isPaid)));
            }
        }

        // ── Build one summary per calendar month ─────────────────────────────

        var summaries = new List<ContributionPeriodSummaryDto>(monthCount);
        for (var m = 0; m < monthCount; m++)
        {
            var mStart = windowStart.AddMonths(m);
            var mEndExclusive = mStart.AddMonths(1);
            var label = mStart.ToString("MMMM yyyy");

            var monthSplits = projected
                .Where(x => x.OccurrenceDate >= mStart && x.OccurrenceDate < mEndExclusive)
                .Select(x => x.Item)
                .OrderBy(i => i.DueDate)
                .ToList();

            var monthPersonal = projectedPersonal
                .Where(x => x.OccurrenceDate >= mStart && x.OccurrenceDate < mEndExclusive)
                .Select(x => x.Item)
                .OrderBy(i => i.DueDate)
                .ToList();

            var totalDue = monthSplits.Sum(s => s.Amount);
            var totalPaid = monthSplits.Where(s => s.IsClaimed).Sum(s => s.Amount);
            var personalDue = monthPersonal.Sum(p => p.Amount);
            var personalPaid = monthPersonal.Where(p => p.IsPaid).Sum(p => p.Amount);

            // Gross and net income — entity methods own the computation
            var projectedIncome = activeSources.Sum(src => src.ProjectGrossForMonth(mStart.Year, mStart.Month));

            var projectedNetIncome = activeSources.Sum(src =>
            {
                var paychecksThisMonth = src.PaychecksInRange(mStart, mEndExclusive);
                if (paychecksThisMonth == 0) return 0m;

                var monthlyNet = PayrollDeductionEngine.ComputeMonthlyNetPay(
                    src.PerPaycheckGross(),
                    src.PaymentFrequency,
                    src.TaxProfile,
                    src.Deductions.Count > 0 ? src.Deductions : null,
                    mStart.Year, mStart.Month);
                var perPaycheckNet = monthlyNet * 12m / src.PaymentFrequency.PeriodsPerYear();
                return perPaycheckNet * paychecksThisMonth;
            });

            decimal? disposableIncome = null;
            string? disposableIncomeSource = null;

            if (now >= mEndExclusive)
            {
                disposableIncome = projectedNetIncome - totalDue - personalDue;
                disposableIncomeSource = "estimate";
            }
            else if (now >= mStart)
            {
                var sharedDueToDate = monthSplits.Where(s => s.DueDate < now).Sum(s => s.Amount);
                var personalDueToDate = monthPersonal.Where(p => p.DueDate < now).Sum(p => p.Amount);
                var incomeReceivedNet = ComputeNetReceivedByCutoff(
                    activeSources, mStart, now);
                disposableIncome = incomeReceivedNet - sharedDueToDate - personalDueToDate;
                disposableIncomeSource = "estimate";
            }

            summaries.Add(new ContributionPeriodSummaryDto(
                label, mStart, mEndExclusive.AddDays(-1),
                totalDue, totalPaid, projectedIncome,
                monthSplits,
                personalDue,
                monthPersonal,
                projectedNetIncome,
                personalPaid,
                disposableIncome,
                disposableIncomeSource));
        }

        return summaries;
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static decimal ComputeNetReceivedByCutoff(
        IEnumerable<IncomeSource> sources,
        DateTime periodStart,
        DateTime cutoffExclusive)
    {
        if (cutoffExclusive <= periodStart) return 0m;
        decimal total = 0m;
        foreach (var src in sources)
        {
            var received = src.PaychecksInRange(periodStart, cutoffExclusive);
            if (received == 0) continue;

            var monthlyNet = PayrollDeductionEngine.ComputeMonthlyNetPay(
                src.PerPaycheckGross(),
                src.PaymentFrequency,
                src.TaxProfile,
                src.Deductions.Count > 0 ? src.Deductions : null,
                periodStart.Year, periodStart.Month);
            var perPaycheckNet = monthlyNet * 12m / src.PaymentFrequency.PeriodsPerYear();
            total += perPaycheckNet * received;
        }
        return total;
    }
}
