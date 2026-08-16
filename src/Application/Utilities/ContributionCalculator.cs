using Finance.Application.Dtos;
using Finance.Domain.Aggregates;
using Finance.Domain.Engines;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Utilities;

// The window projects future months, whose occurrences are never posted, so the two halves of each
// item come from different sources by design:
//
//   Amounts due are a recurrence-schedule projection over the expense/share rows. It is a
//   schedule projection, NOT a ledger read, and cannot be derived from the ledger.
//
//   Settled/paid status always comes from the ledger. The lone exception is the payer's own share,
//   which is implicitly covered by fronting the expense and so has no payment row.
internal sealed class ContributionCalculator : IContributionCalculator
{
    private readonly IPayrollDeductionEngine _deductionEngine;

    public ContributionCalculator(IPayrollDeductionEngine deductionEngine)
    {
        _deductionEngine = deductionEngine;
    }

    public IReadOnlyCollection<ContributionPeriodSummaryDto> BuildSummaries(
        DateTime now,
        int monthCount,
        int pastMonths,
        IReadOnlyList<IncomeSource> incomeSources,
        IReadOnlyList<Expense> personalExpenses,
        IReadOnlyList<(Share Share, Expense Expense)> shares,
        IReadOnlyDictionary<(Guid ShareId, DateTime OccurrenceDate), DateTime> paidShareOccurrences,
        IReadOnlyDictionary<(Guid ExpenseId, DateTime OccurrenceDate), DateTime> paidPersonalExpenseOccurrences)
    {
        var windowStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-pastMonths);
        var windowEndExclusive = windowStart.AddMonths(monthCount);

        var activeSources = incomeSources.Where(s => s.IsActive).ToList();
        var activePersonal = personalExpenses.Where(e => e.IsActive).ToList();

        var projected = new List<(DateTime OccurrenceDate, ContributionItemDto Item)>();
        foreach (var s in shares)
        {
            // One expense, one occurrence. A repeating expense arrives here as a row per period,
            // generated when that period came round, so nothing is projected.
            IEnumerable<DateTime> occurrenceDates =
                s.Expense.OccurrenceDate >= windowStart && s.Expense.OccurrenceDate < windowEndExclusive
                    ? [s.Expense.OccurrenceDate]
                    : [];

            // Fronting the expense covers your own part of it, so there is no payment record and
            // hence no PaidAt. The expense answers this rather than two of its fields being
            // compared here, which is how the same rule ended up spelled out in three places.
            var isPayerOwnShare = s.Expense.CoversOwnShare(s.Share.UserId.Value);

            foreach (var date in occurrenceDates)
            {
                var hasPayment = paidShareOccurrences.TryGetValue((s.Share.Id.Value, date.Date), out var paidAt);
                var isPaid = hasPayment || isPayerOwnShare;
                projected.Add((date, new ContributionItemDto(
                    s.Share.Id.Value, s.Expense.Id.Value, s.Expense.Title, s.Expense.Category.ToString(),
                    s.Share.Amount.Amount, s.Share.Amount.Currency, date,
                    isPaid,
                    s.Expense.GroupId!.Value.Value,
                    hasPayment ? paidAt : null)));
            }
        }

        var projectedPersonal = new List<(DateTime OccurrenceDate, PersonalExpenseItemDto Item)>();
        foreach (var e in activePersonal)
        {
            IEnumerable<DateTime> occurrenceDates =
                e.OccurrenceDate >= windowStart && e.OccurrenceDate < windowEndExclusive
                    ? [e.OccurrenceDate]
                    : [];

            foreach (var date in occurrenceDates)
            {
                var isPaid = paidPersonalExpenseOccurrences.ContainsKey((e.Id.Value, date.Date));
                projectedPersonal.Add((date, new PersonalExpenseItemDto(
                    e.Id.Value, e.Title, e.Category.ToString(),
                    e.Amount.Amount, e.Amount.Currency, date, isPaid)));
            }
        }

        var summaries = new List<ContributionPeriodSummaryDto>(monthCount);
        for (var m = 0; m < monthCount; m++)
        {
            var mStart = windowStart.AddMonths(m);
            var mEndExclusive = mStart.AddMonths(1);
            var label = mStart.ToString("MMMM yyyy");

            var monthShares = projected
                .Where(x => x.OccurrenceDate >= mStart && x.OccurrenceDate < mEndExclusive)
                .Select(x => x.Item)
                .OrderBy(i => i.DueDate)
                .ToList();

            var monthPersonal = projectedPersonal
                .Where(x => x.OccurrenceDate >= mStart && x.OccurrenceDate < mEndExclusive)
                .Select(x => x.Item)
                .OrderBy(i => i.DueDate)
                .ToList();

            var totalDue = monthShares.Sum(s => s.Amount);
            var totalPaid = monthShares.Where(s => s.IsPaid).Sum(s => s.Amount);
            var personalDue = monthPersonal.Sum(p => p.Amount);
            var personalPaid = monthPersonal.Where(p => p.IsPaid).Sum(p => p.Amount);

            var projectedIncome = activeSources.Sum(src => src.ProjectGrossForMonth(mStart.Year, mStart.Month));

            var projectedNetIncome = activeSources.Sum(src =>
            {
                var paychecksThisMonth = src.PaychecksInRange(mStart, mEndExclusive);
                if (paychecksThisMonth == 0) return 0m;

                var monthlyNet = _deductionEngine.ComputeMonthlyNetPay(src, mStart.Year, mStart.Month);
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
                var sharedDueToDate = monthShares.Where(s => s.DueDate < now).Sum(s => s.Amount);
                var personalDueToDate = monthPersonal.Where(p => p.DueDate < now).Sum(p => p.Amount);
                var incomeReceivedNet = ComputeNetReceivedByCutoff(activeSources, mStart, now);
                disposableIncome = incomeReceivedNet - sharedDueToDate - personalDueToDate;
                disposableIncomeSource = "estimate";
            }

            summaries.Add(new ContributionPeriodSummaryDto(
                label, mStart, mEndExclusive.AddDays(-1),
                totalDue, totalPaid, projectedIncome,
                monthShares,
                personalDue,
                monthPersonal,
                projectedNetIncome,
                personalPaid,
                disposableIncome,
                disposableIncomeSource));
        }

        return summaries;
    }

    private decimal ComputeNetReceivedByCutoff(
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

            var monthlyNet = _deductionEngine.ComputeMonthlyNetPay(src, periodStart.Year, periodStart.Month);
            var perPaycheckNet = monthlyNet * 12m / src.PaymentFrequency.PeriodsPerYear();
            total += perPaycheckNet * received;
        }
        return total;
    }
}
