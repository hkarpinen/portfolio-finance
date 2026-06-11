using Finance.Application.Dtos;
using Finance.Domain.Aggregates;
using Finance.Domain.Engines;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Utilities;

/// <summary>
/// Builds the forward-looking contributions view (one summary per calendar month across a window
/// that spans future months).
/// <para>
/// <b>Derivation contract.</b> The contributions window deliberately projects future occurrences,
/// which do not exist in the ledger (only the current accrual is ever posted). So the two halves of
/// each item come from different sources, by design:
/// </para>
/// <list type="bullet">
///   <item><description><b>Amounts due</b> (future-month projections) come from the charge /
///   allocation <i>rows</i> — the recurrence schedule projected over the window. This is a schedule
///   projection, NOT a ledger read, and cannot be derived from the ledger.</description></item>
///   <item><description><b>Settled / paid status</b> always comes from the ledger
///   (<c>SettlementReads</c> / <c>VendorPaymentReads</c>, surfaced here as
///   <c>paidAllocationOccurrences</c> / <c>paidPersonalBillOccurrences</c>). The lone exception is
///   the payer's own share, which is implicitly covered by fronting the bill and so has no payment
///   row — mirrored from <c>ChargeQuery.ListAllocationsByGroupAsync</c> so both views agree.</description></item>
/// </list>
/// This is why <c>/balances</c> (a pure ledger derivation) and this view can legitimately differ on
/// future months yet must agree on settled status for posted occurrences.
/// </summary>
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
        IReadOnlyList<Charge> personalCharges,
        IReadOnlyList<(Allocation Allocation, Charge Charge)> splits,
        IReadOnlyDictionary<(Guid AllocationId, DateTime OccurrenceDate), DateTime> paidAllocationOccurrences,
        IReadOnlyDictionary<(Guid ChargeId, DateTime OccurrenceDate), DateTime> paidPersonalBillOccurrences)
    {
        var windowStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-pastMonths);
        var windowEndExclusive = windowStart.AddMonths(monthCount);

        var activeSources = incomeSources.Where(s => s.IsActive).ToList();
        var activePersonal = personalCharges.Where(e => e.IsActive).ToList();

        // ── Project split occurrences across the window ──────────────────────

        var projected = new List<(DateTime OccurrenceDate, ContributionItemDto Item)>();
        foreach (var s in splits)
        {
            IEnumerable<DateTime> occurrenceDates = s.Charge.RecurrenceSchedule is not null
                ? s.Charge.RecurrenceSchedule.GetOccurrencesInRange(windowStart, windowEndExclusive)
                : (IEnumerable<DateTime>)[s.Charge.DueDate];

            // The payer's own share is covered by paying the bill — they never
            // reimburse themselves. Mirror of the household-contributions read
            // (ChargeQuery.ListAllocationsByGroupAsync) so both views agree. A
            // payer-covered share has no explicit payment record, hence no PaidAt.
            var isPayerOwnShare = s.Charge.PayerUserId == s.Allocation.UserId.Value;

            foreach (var date in occurrenceDates)
            {
                var hasPayment = paidAllocationOccurrences.TryGetValue((s.Allocation.Id.Value, date.Date), out var paidAt);
                var isPaid = hasPayment || isPayerOwnShare;
                projected.Add((date, new ContributionItemDto(
                    s.Allocation.Id.Value, s.Charge.Id.Value, s.Charge.Title, s.Charge.Category.ToString(),
                    s.Allocation.Amount.Amount, s.Allocation.Amount.Currency, date,
                    isPaid,
                    s.Allocation.GroupId.Value,
                    hasPayment ? paidAt : null)));
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

            var monthAllocations = projected
                .Where(x => x.OccurrenceDate >= mStart && x.OccurrenceDate < mEndExclusive)
                .Select(x => x.Item)
                .OrderBy(i => i.DueDate)
                .ToList();

            var monthPersonal = projectedPersonal
                .Where(x => x.OccurrenceDate >= mStart && x.OccurrenceDate < mEndExclusive)
                .Select(x => x.Item)
                .OrderBy(i => i.DueDate)
                .ToList();

            var totalDue = monthAllocations.Sum(s => s.Amount);
            var totalPaid = monthAllocations.Where(s => s.IsPaid).Sum(s => s.Amount);
            var personalDue = monthPersonal.Sum(p => p.Amount);
            var personalPaid = monthPersonal.Where(p => p.IsPaid).Sum(p => p.Amount);

            var projectedIncome = activeSources.Sum(src => src.ProjectGrossForMonth(mStart.Year, mStart.Month));

            var projectedNetIncome = activeSources.Sum(src =>
            {
                var paychecksThisMonth = src.PaychecksInRange(mStart, mEndExclusive);
                if (paychecksThisMonth == 0) return 0m;

                var monthlyNet = _deductionEngine.ComputeMonthlyNetPay(
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
                var sharedDueToDate = monthAllocations.Where(s => s.DueDate < now).Sum(s => s.Amount);
                var personalDueToDate = monthPersonal.Where(p => p.DueDate < now).Sum(p => p.Amount);
                var incomeReceivedNet = ComputeNetReceivedByCutoff(activeSources, mStart, now);
                disposableIncome = incomeReceivedNet - sharedDueToDate - personalDueToDate;
                disposableIncomeSource = "estimate";
            }

            summaries.Add(new ContributionPeriodSummaryDto(
                label, mStart, mEndExclusive.AddDays(-1),
                totalDue, totalPaid, projectedIncome,
                monthAllocations,
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

            var monthlyNet = _deductionEngine.ComputeMonthlyNetPay(
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
