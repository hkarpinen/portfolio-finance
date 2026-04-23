using Bills.Application.Contracts;
using Bills.Application.Queries;
using Bills.Domain.ValueObjects;
using Client.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Client.Controllers;

/// <summary>
/// Single-call overview endpoint that replaces the N+1 fan-out of household + per-household
/// dashboard + bills + income calls previously orchestrated by the frontend.
/// </summary>
[ApiController]
[Authorize]
[Route("api/bills/overview")]
public sealed class OverviewController : ControllerBase
{
    private readonly IHouseholdQuery _householdQuery;
    private readonly IHouseholdMembershipQuery _membershipQuery;
    private readonly IBillQuery _billQuery;
    private readonly IDashboardQuery _dashboardQuery;
    private readonly IIncomeQuery _incomeQuery;
    private readonly IBillSplitQuery _splitQuery;

    public OverviewController(
        IHouseholdQuery householdQuery,
        IHouseholdMembershipQuery membershipQuery,
        IBillQuery billQuery,
        IDashboardQuery dashboardQuery,
        IIncomeQuery incomeQuery,
        IBillSplitQuery splitQuery)
    {
        _householdQuery = householdQuery;
        _membershipQuery = membershipQuery;
        _billQuery = billQuery;
        _dashboardQuery = dashboardQuery;
        _incomeQuery = incomeQuery;
        _splitQuery = splitQuery;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var userId = User.GetUserId().Value;

        var now = DateTime.UtcNow;
        var periodStart = new DateTime(now.Year, now.Month, 1);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var upcomingCutoff = now.AddDays(7);

        // 1. Fetch all households the user belongs to
        var householdList = await _householdQuery.ListAsync(
            new ListHouseholdsRequest(userId, 1, 100, ActiveOnly: true), ct);
        var households = householdList.Items.ToList();

        // 2. Per-household: members count + dashboard + bills — sequential to avoid EF concurrency issues
        var memberCounts = new List<int>();
        var dashboards = new List<DashboardResponse>();
        var billsLists = new List<BillListResponse>();

        foreach (var h in households)
        {
            var members = await _membershipQuery.ListMembersAsync(h.HouseholdId, ct);
            memberCounts.Add(members.Count);

            var dash = await _dashboardQuery.QueryAsync(new DashboardQueryRequest(h.HouseholdId, periodStart, periodEnd), ct);
            dashboards.Add(dash);

            var bills = await _billQuery.ListAsync(new ListBillsRequest(h.HouseholdId, 1, 500, true), ct);
            billsLists.Add(bills);
        }

        // 3. User-level income
        var incomeResult = await _incomeQuery.ListByUserAsync(new ListUserIncomeRequest(userId, 1, 500, true), ct);
        var income = incomeResult.Items.Where(s => s.IsActive).ToList();

        // 4. Build household summary items
        var summaryItems = households.Select((h, i) =>
        {
            var dash = dashboards[i];
            var memberCount = memberCounts[i];
            return new HouseholdSummaryItem(
                h.HouseholdId,
                h.Name,
                h.Description,
                h.CurrencyCode,
                h.OwnerId,
                memberCount,
                dash.TotalBills,
                dash.TotalIncome,
                dash.NetBalance,
                dash.IsOvercommitted);
        }).ToList();

        // 5. Build upcoming bills (next 7 days across all households)
        var upcomingBills = new List<UpcomingBillItem>();
        for (var i = 0; i < households.Count; i++)
        {
            var h = households[i];
            foreach (var bill in billsLists[i].Items)
            {
                if (bill.IsActive && bill.DueDate >= now && bill.DueDate <= upcomingCutoff)
                {
                    upcomingBills.Add(new UpcomingBillItem(
                        bill.BillId, bill.HouseholdId, h.Name,
                        bill.Title, bill.Amount, bill.Currency, bill.DueDate));
                }
            }
        }
        upcomingBills.Sort((a, b) => a.DueDate.CompareTo(b.DueDate));

        // 6. Compute total monthly income (average across all months — for header display)
        decimal totalMonthlyIncome = income.Sum(src => MonthlyEquivalent(src));

        // 7. Build 12-month contribution summaries (3 past months + current + 8 future)
        var contributionsByMonth = await BuildContributionSummariesAsync(
            userId, income, now, monthCount: 12, pastMonths: 3, ct);

        return Ok(new UserBillsOverviewResponse(
            summaryItems,
            upcomingBills,
            totalMonthlyIncome,
            contributionsByMonth,
            income));
    }

    // ─── Contribution summary builder ───────────────────────────────────────

    private async Task<IReadOnlyCollection<ContributionPeriodSummary>> BuildContributionSummariesAsync(
        Guid userId,
        IReadOnlyCollection<IncomeResponse> incomeSources,
        DateTime now,
        int monthCount,
        int pastMonths,
        CancellationToken ct)
    {
        var windowStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-pastMonths);
        var windowEnd = windowStart.AddMonths(monthCount).AddDays(-1);

        var userIdVo = UserId.Create(userId);
        var splits = await _splitQuery.ListByUserWithBillDetailsAsync(userIdVo, windowStart, windowEnd, ct);

        // Expand each split into all its occurrences within the window.
        // Recurring splits produce one ContributionItem per occurrence date;
        // one-time splits produce exactly one item at their DueDate.
        var projected = new List<(DateTime OccurrenceDate, ContributionItem Item)>();
        foreach (var s in splits)
        {
            IEnumerable<DateTime> occurrenceDates;
            if (s.RecurrenceFrequency.HasValue && s.RecurrenceStartDate.HasValue)
            {
                var schedule = RecurrenceSchedule.Create(
                    s.RecurrenceFrequency.Value,
                    s.RecurrenceStartDate.Value,
                    s.RecurrenceEndDate);
                // windowEnd is the last day of the last month — add 1 day so the range is exclusive-end
                occurrenceDates = schedule.GetOccurrencesInRange(windowStart, windowEnd.AddDays(1));
            }
            else
            {
                occurrenceDates = [s.DueDate];
            }

            foreach (var date in occurrenceDates)
            {
                // Only the occurrence that matches the bill's original DueDate month carries the
                // actual IsClaimed flag — future projected occurrences are always unclaimed.
                var isOriginalMonth = date.Year == s.DueDate.Year && date.Month == s.DueDate.Month;
                projected.Add((date, new ContributionItem(
                    s.SplitId, s.BillId, s.HouseholdId, s.HouseholdName,
                    s.BillTitle, s.BillCategory, s.Amount, s.Currency,
                    date,
                    isOriginalMonth && s.IsClaimed,
                    isOriginalMonth ? s.ClaimedAt : null)));
            }
        }

        var summaries = new List<ContributionPeriodSummary>(monthCount);
        for (var m = 0; m < monthCount; m++)
        {
            var mStart = windowStart.AddMonths(m);
            var mEnd = mStart.AddMonths(1).AddDays(-1);
            var label = mStart.ToString("MMMM yyyy");

            var monthSplits = projected
                .Where(x => x.OccurrenceDate >= mStart && x.OccurrenceDate <= mEnd)
                .Select(x => x.Item)
                .OrderBy(i => i.DueDate)
                .ToList();

            var totalDue = monthSplits.Sum(s => s.Amount);
            var totalPaid = monthSplits.Where(s => s.IsClaimed).Sum(s => s.Amount);
            var projectedIncome = incomeSources.Sum(src => ProjectIncomeForMonth(src, mStart.Year, mStart.Month));

            summaries.Add(new ContributionPeriodSummary(
                label, mStart, mEnd,
                totalDue, totalPaid, projectedIncome,
                projectedIncome - totalDue,
                monthSplits));
        }

        return summaries;
    }

    // ─── Income projection helpers ───────────────────────────────────────────

    /// <summary>
    /// Returns the monthly-equivalent income from <paramref name="src"/> for the given month.
    /// Uses a budget model (not cash-flow): annual/quarterly/semi-annual sources are normalised
    /// to a per-month average so every month shows a consistent share of available income.
    /// Returns 0 if the source hasn't started yet or has already ended.
    /// </summary>
    private static decimal ProjectIncomeForMonth(IncomeResponse src, int year, int month)
    {
        if (!src.IsActive) return 0m;

        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        if (src.StartDate > monthEnd) return 0m;
        if (src.EndDate.HasValue && src.EndDate.Value < monthStart) return 0m;

        return MonthlyEquivalent(src);
    }

    private static decimal MonthlyEquivalent(IncomeResponse src) => src.Frequency switch
    {
        RecurrenceFrequency.Weekly       => src.Amount * 52m / 12m,
        RecurrenceFrequency.BiWeekly     => src.Amount * 26m / 12m,
        RecurrenceFrequency.Annually     => src.Amount / 12m,
        RecurrenceFrequency.Quarterly    => src.Amount / 3m,
        RecurrenceFrequency.SemiAnnually => src.Amount / 6m,
        _                                => src.Amount
    };

    /// <summary>
    /// Returns true if (year, month) is exactly a multiple of <paramref name="intervalMonths"/>
    /// calendar months away from <paramref name="start"/>.
    /// </summary>
    private static bool IsNthMonthFromStart(DateTime start, int year, int month, int intervalMonths)
    {
        var totalMonthsFromStart = (year * 12 + month) - (start.Year * 12 + start.Month);
        return totalMonthsFromStart >= 0 && totalMonthsFromStart % intervalMonths == 0;
    }
}
