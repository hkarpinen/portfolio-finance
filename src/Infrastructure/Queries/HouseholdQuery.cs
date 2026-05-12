using Finance.Application.Dtos;
using Finance.Application.Queries;
using Finance.Application.Mappers;
using Finance.Application.Ports;
using Finance.Application.Utilities;
using Finance.Domain.Aggregates;
using Finance.Domain.Engines;
using Finance.Infrastructure.Persistence.Projections;
using Finance.Domain.Utilities;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class HouseholdQuery : IHouseholdQuery
{
    private readonly FinanceDbContext _db;

    public HouseholdQuery(
        FinanceDbContext db)
    {
        _db = db;
    }

    // ── Household list / detail ───────────────────────────────────────────────

    public async Task<HouseholdListDto> ListAsync(ListHouseholdsParams request, CancellationToken cancellationToken = default)
    {
        var query = _db.Households.AsNoTracking().AsQueryable();
        if (request.ActiveOnly) query = query.Where(h => h.IsActive);
        if (request.UserId.HasValue)
        {
            var memberHouseholdIds = await _db.HouseholdMemberships
                .AsNoTracking()
                .Where(m => m.UserId == UserId.Create(request.UserId.Value) && m.IsActive)
                .Select(m => m.HouseholdId)
                .ToListAsync(cancellationToken);
            query = query.Where(h => memberHouseholdIds.Contains(h.Id));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(h => h.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new HouseholdListDto(items.Select(HouseholdMapper.ToResponse).ToArray(), total);
    }

    public async Task<HouseholdDto?> GetDetailAsync(HouseholdDetailParams request, CancellationToken cancellationToken = default)
    {
        var household = await _db.Households.AsNoTracking().FirstOrDefaultAsync(h => h.Id == HouseholdId.Create(request.HouseholdId), cancellationToken);
        return household is null ? null : HouseholdMapper.ToResponse(household);
    }

    public async Task<HouseholdDetailDto?> GetPageAsync(
        Guid householdId,
        Guid userId,
        DateTime periodStart,
        DateTime periodEnd,
        CancellationToken cancellationToken = default)
    {
        var household = await GetDetailAsync(new HouseholdDetailParams(householdId), cancellationToken);
        if (household is null) return null;

        var hid = HouseholdId.Create(householdId);

        var memberEntities = await _db.HouseholdMemberships
            .AsNoTracking()
            .Where(m => m.HouseholdId == hid && m.IsActive)
            .ToListAsync(cancellationToken);
        var memberUserIds = memberEntities.Select(m => m.UserId).ToList();
        var projections = await _db.UserProjections
            .AsNoTracking()
            .Where(p => memberUserIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);
        var projDict = projections.ToDictionary(p => p.UserId);
        var members = memberEntities.Select(m =>
        {
            projDict.TryGetValue(m.UserId, out var proj);
            return MembershipMapper.ToResponse(m, proj?.GetFullName());
        }).ToList();

        var billEntities = await _db.Expenses
            .AsNoTracking()
            .Where(b => b.HouseholdId == hid && b.IsActive)
            .OrderBy(b => b.DueDate)
            .ToListAsync(cancellationToken);
        var bills = billEntities.Select(b => ExpenseMapper.ToHouseholdResponse(b)).ToList();

        var dashboard = await QueryAsync(
            new DashboardParams(householdId, periodStart, periodEnd), cancellationToken);

        // Inline income list (was: _incomeQuery.ListByUserAsync)
        var userIncomeItems = await _db.IncomeSources
            .AsNoTracking()
            .Where(i => i.UserId == UserId.Create(userId) && i.IsActive)
            .OrderBy(i => i.Source)
            .Take(500)
            .ToListAsync(cancellationToken);
        var userIncomeDtos = userIncomeItems.Select(IncomeMapper.ToResponse).ToList();

        var monthlyIncome = userIncomeDtos
            .Where(s => s.IsActive
                && s.StartDate <= periodEnd
                && (!s.EndDate.HasValue || s.EndDate.Value >= periodStart))
            .Sum(src => UserBudgetCalculator.PerPaycheckAmount(src.Amount, src.QuotedAs, src.PaidEvery)
                    * RecurrenceSchedule.Create(src.PaidEvery, src.LastPaycheckDate ?? src.StartDate, src.EndDate)
                        .GetOccurrencesInRange(periodStart, periodEnd.AddDays(1)).Count);

        // Inline split-with-bill-details query (was: _expenseQuery.ListByUserWithBillDetailsAsync)
        var userSplits = await GetSplitsWithBillDetailsAsync(UserId.Create(userId), periodStart, periodEnd.AddDays(1), cancellationToken);
        var monthlyObligations = UserBudgetCalculator.MonthlyObligationsForUser(
            userSplits, periodStart.Year, periodStart.Month);
        var userNetBalance = monthlyIncome - monthlyObligations;

        var correctedDashboard = dashboard with
        {
            TotalGrossIncome = monthlyIncome,
            TotalNetIncome = monthlyIncome,
            NetBalance = userNetBalance,
            IsOvercommitted = userNetBalance < 0,
        };

        return new HouseholdDetailDto(household, members, bills, correctedDashboard);
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    public async Task<DashboardDto> QueryAsync(DashboardParams request, CancellationToken cancellationToken = default)
    {
        var (grossIncome, netIncome) = await GetIncomeAsync(
            request.HouseholdId, request.PeriodStart, request.PeriodEnd, cancellationToken);
        var totalBills = await GetTotalBillsAsync(
            request.HouseholdId, request.PeriodStart, request.PeriodEnd, cancellationToken);
        var balanceSummary = await GetHouseholdBalanceAsync(request.HouseholdId, cancellationToken);

        var isOvercommitted = totalBills.Amount > netIncome.Amount;

        var domainCoverage = HouseholdCoverageEngine.BuildCoverageStatus(
            request.HouseholdId, grossIncome, netIncome, totalBills,
            request.PeriodStart, request.PeriodEnd);
        var coverage = ToCoverageStatusDto(domainCoverage);

        return new DashboardDto(
            request.HouseholdId,
            grossIncome.Amount,
            netIncome.Amount,
            totalBills.Amount,
            netIncome.Amount - totalBills.Amount,
            isOvercommitted,
            coverage,
            request.PeriodStart,
            request.PeriodEnd,
            balanceSummary.HasConnectedAccounts ? balanceSummary.TotalAvailable : null,
            balanceSummary.HasConnectedAccounts ? balanceSummary.AsOf : null);
    }

    public async Task<CoverageStatusDto> GetCoverageStatusAsync(CoverageStatusParams request, CancellationToken cancellationToken = default)
    {
        var (grossIncome, netIncome) = await GetIncomeAsync(
            request.HouseholdId, request.PeriodStart, request.PeriodEnd, cancellationToken);
        var totalBills = await GetTotalBillsAsync(
            request.HouseholdId, request.PeriodStart, request.PeriodEnd, cancellationToken);
        return ToCoverageStatusDto(
            HouseholdCoverageEngine.BuildCoverageStatus(
                request.HouseholdId, grossIncome, netIncome, totalBills,
                request.PeriodStart, request.PeriodEnd));
    }

    private static CoverageStatusDto ToCoverageStatusDto(CoverageStatus cs) =>
        new(cs.HouseholdId, cs.TotalGrossIncomeAmount, cs.TotalNetIncomeAmount,
            cs.TotalBillsAmount, cs.Ratio, cs.IsFullyCovered, cs.Status,
            cs.PeriodStart, cs.PeriodEnd);

    private async Task<(Money Gross, Money Net)> GetIncomeAsync(
        Guid householdId, DateTime periodStart, DateTime periodEnd,
        CancellationToken cancellationToken)
    {
        var memberUserIds = await _db.HouseholdMemberships
            .AsNoTracking()
            .Where(m => m.HouseholdId == HouseholdId.Create(householdId) && m.IsActive)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        var items = await _db.IncomeSources
            .AsNoTracking()
            .Where(i => memberUserIds.Contains(i.UserId) && i.IsActive)
            .ToListAsync(cancellationToken);

        decimal gross = 0, net = 0;
        string currency = "USD";

        var periodMonths = Math.Max(1,
            (periodEnd.Year * 12 + periodEnd.Month) - (periodStart.Year * 12 + periodStart.Month) + 1);

        var periodEndExclusive = new DateTime(periodEnd.Year, periodEnd.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(1);

        foreach (var income in items)
        {
            currency = income.Amount.Currency;
            if (income.RecurrenceSchedule.StartDate > periodEnd) continue;
            if (income.RecurrenceSchedule.EndDate.HasValue &&
                income.RecurrenceSchedule.EndDate.Value < periodStart) continue;

            gross += UserBudgetCalculator.MonthlyEquivalent(
                income.Amount.Amount, income.RecurrenceSchedule.Frequency) * periodMonths;

            var anchor = income.LastPaymentDate ?? income.RecurrenceSchedule.StartDate;
            var schedule = RecurrenceSchedule.Create(income.PaymentFrequency, anchor, income.RecurrenceSchedule.EndDate);
            var paychecksInPeriod = schedule.GetOccurrencesInRange(periodStart, periodEndExclusive).Count;
            if (paychecksInPeriod == 0) continue;

            var perPaycheckGross = UserBudgetCalculator.PerPaycheckAmount(
                income.Amount.Amount,
                income.RecurrenceSchedule.Frequency,
                income.PaymentFrequency);
            var monthlyNetAtCadence = PayrollDeductionEngine.ComputeMonthlyNetPay(
                perPaycheckGross, income.PaymentFrequency,
                income.TaxProfile,
                income.Deductions.Count > 0 ? income.Deductions : null,
                periodStart.Year, periodStart.Month);
            var perPaycheckNet = monthlyNetAtCadence * 12m / income.PaymentFrequency.PeriodsPerYear();
            net += perPaycheckNet * paychecksInPeriod;
        }

        return (Money.Create(gross, currency), Money.Create(net, currency));
    }

    private async Task<Money> GetTotalBillsAsync(Guid householdId, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken)
    {
        var items = await _db.Expenses
            .AsNoTracking()
            .Where(b => b.HouseholdId == HouseholdId.Create(householdId) && b.IsActive)
            .ToListAsync(cancellationToken);

        decimal total = 0;
        string currency = "USD";

        foreach (var bill in items)
        {
            currency = bill.Amount.Currency;
            var recurrence = bill.RecurrenceSchedule;

            if (recurrence == null)
            {
                if (bill.DueDate >= periodStart && bill.DueDate <= periodEnd)
                    total += bill.Amount.Amount;
                continue;
            }

            total += recurrence.GetAmountForPeriod(bill.Amount, periodStart, periodEnd);
        }

        return Money.Create(total, currency);
    }

    private async Task<AccountBalanceSummaryDto> GetHouseholdBalanceAsync(
        Guid householdId, CancellationToken cancellationToken)
    {
        var memberUserIds = await _db.HouseholdMemberships
            .AsNoTracking()
            .Where(m => m.HouseholdId == HouseholdId.Create(householdId) && m.IsActive)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        var accounts = await _db.FinancialAccounts
            .AsNoTracking()
            .Where(a => memberUserIds.Contains(a.UserId) && a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

        if (accounts.Count == 0)
            return new AccountBalanceSummaryDto(null, null, null, false, []);

        var spendable = accounts
            .Where(a => string.Equals(a.Type, "depository", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var currency = spendable.FirstOrDefault()?.CurrencyCode ?? accounts[0].CurrencyCode;
        decimal? totalAvailable = spendable.Any(a => a.AvailableBalance.HasValue)
            ? spendable.Sum(a => a.AvailableBalance ?? 0m)
            : null;
        var asOf = accounts.Max(a => (DateTime?)a.UpdatedAt);

        var dtos = accounts.Select(a => new LinkedAccountBalanceDto(
            a.Id, a.Name, a.Mask, a.Type,
            a.AvailableBalance, a.CurrentBalance, a.CurrencyCode)).ToList();

        return new AccountBalanceSummaryDto(totalAvailable, currency, asOf, true, dtos);
    }

    // ── Membership ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyCollection<MembershipDto>> ListMembersAsync(
        Guid householdId, CancellationToken cancellationToken = default)
    {
        var hid = HouseholdId.Create(householdId);

        var members = await _db.HouseholdMemberships
            .AsNoTracking()
            .Where(m => m.HouseholdId == hid && m.IsActive)
            .ToListAsync(cancellationToken);

        var userIds = members.Select(m => m.UserId).ToList();

        var projections = await _db.UserProjections
            .AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);

        var projDict = projections.ToDictionary(p => p.UserId);

        return members.Select(m =>
        {
            projDict.TryGetValue(m.UserId, out var proj);
            return MembershipMapper.ToResponse(m, proj?.GetFullName());
        }).ToList();
    }

    // ── User overview ─────────────────────────────────────────────────────────

    public async Task<UserOverviewDto> GetUserOverviewAsync(Guid userId, DateTime now, CancellationToken cancellationToken = default)
    {
        var periodStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);
        var upcomingCutoff = now.AddDays(7);
        var uid = UserId.Create(userId);

        var memberHouseholdIds = await _db.HouseholdMemberships
            .AsNoTracking()
            .Where(m => m.UserId == uid && m.IsActive)
            .Select(m => m.HouseholdId)
            .ToListAsync(cancellationToken);

        var householdEntities = await _db.Households
            .AsNoTracking()
            .Where(h => memberHouseholdIds.Contains(h.Id) && h.IsActive)
            .OrderBy(h => h.Name)
            .ToListAsync(cancellationToken);

        var householdSummaries = new List<HouseholdSummaryDto>(householdEntities.Count);
        var upcomingBills = new List<UpcomingBillDto>();

        foreach (var h in householdEntities)
        {
            var memberCount = await _db.HouseholdMemberships
                .AsNoTracking()
                .CountAsync(m => m.HouseholdId == h.Id && m.IsActive, cancellationToken);

            var dashboard = await QueryAsync(
                new DashboardParams(h.Id.Value, periodStart, periodEnd), cancellationToken);

            var bills = await _db.Expenses
                .AsNoTracking()
                .Where(b => b.HouseholdId == h.Id && b.IsActive)
                .OrderBy(b => b.DueDate)
                .ToListAsync(cancellationToken);

            foreach (var bill in bills)
            {
                if (bill.DueDate >= now && bill.DueDate <= upcomingCutoff)
                    upcomingBills.Add(new UpcomingBillDto(
                        bill.Id.Value, h.Id.Value, h.Name,
                        bill.Title, bill.Amount.Amount, bill.Amount.Currency, bill.DueDate));
            }

            householdSummaries.Add(new HouseholdSummaryDto(
                h.Id.Value, h.Name, h.Description, h.CurrencyCode, h.OwnerId.Value,
                memberCount,
                dashboard.TotalBills, dashboard.TotalGrossIncome, dashboard.NetBalance, dashboard.IsOvercommitted));
        }

        // Inline income list (was: _incomeQuery.GetAllActiveByUserAsync)
        var incomeEntities = await _db.IncomeSources
            .AsNoTracking()
            .Where(i => i.UserId == uid && i.IsActive)
            .OrderBy(i => i.Source)
            .ToListAsync(cancellationToken);
        var income = incomeEntities.Select(IncomeMapper.ToResponse).ToList();

        // Inline personal bills list (was: _expenseQuery.GetAllActivePersonalByUserAsync)
        var personalBillEntities = await _db.Expenses
            .AsNoTracking()
            .Where(e => e.UserId == uid && e.IsActive && e.HouseholdId == null)
            .OrderBy(e => e.DueDate)
            .ToListAsync(cancellationToken);
        var personalBillDtos = personalBillEntities.Select(e => ExpenseMapper.ToResponse(e)).ToList();

        foreach (var pb in personalBillDtos)
        {
            if (pb.DueDate >= now && pb.DueDate <= upcomingCutoff)
                upcomingBills.Add(new UpcomingBillDto(
                    pb.ExpenseId, Guid.Empty, "Personal",
                    pb.Title, pb.Amount, pb.Currency, pb.DueDate));
        }
        upcomingBills.Sort((a, b) => a.DueDate.CompareTo(b.DueDate));

        var thisMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var thisMonthEnd = thisMonthStart.AddMonths(1);
        decimal totalMonthlyIncome = incomeEntities
            .Where(src => src.IsActive)
            .Sum(src => src.ProjectGrossForMonth(thisMonthStart.Year, thisMonthStart.Month));

        decimal totalPersonalBillsMonthly = personalBillEntities
            .Where(e => e.IsActive)
            .Sum(e => e.RecurrenceSchedule is not null
                ? UserBudgetCalculator.MonthlyEquivalent(e.Amount.Amount, e.RecurrenceSchedule.Frequency)
                : e.Amount.Amount);

        // Contribution summaries
        var windowStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-3);
        var queryWindowEnd = windowStart.AddMonths(12).AddDays(-1);

        var splits = await GetSplitsWithBillDetailsAsync(uid, windowStart, queryWindowEnd, cancellationToken);
        var paidSplits = await GetPaidSplitOccurrencesAsync(uid, windowStart, queryWindowEnd, cancellationToken);
        var paidPersonal = await GetPaidPersonalBillOccurrencesAsync(uid, windowStart, queryWindowEnd, cancellationToken);

        var contributionsByMonth = ContributionCalculator.BuildSummaries(
            now, monthCount: 12, pastMonths: 3,
            incomeEntities, personalBillEntities,
            splits, paidSplits, paidPersonal);

        var currentMonthSummary = contributionsByMonth
            .FirstOrDefault(c => c.PeriodStart.Year == periodStart.Year && c.PeriodStart.Month == periodStart.Month);

        decimal totalMonthlyNetIncome = currentMonthSummary?.ProjectedNetIncome ?? totalMonthlyIncome;
        var userNetBalance = totalMonthlyIncome - (currentMonthSummary?.TotalDue ?? 0m);

        var correctedSummaries = householdSummaries
            .Select(s => s with { TotalGrossIncome = totalMonthlyIncome, NetBalance = userNetBalance, IsOvercommitted = userNetBalance < 0 })
            .ToList();

        return new UserOverviewDto(
            correctedSummaries,
            upcomingBills,
            totalMonthlyIncome,
            contributionsByMonth,
            income,
            totalPersonalBillsMonthly,
            totalMonthlyNetIncome);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Inline of ExpenseQuery.ListByUserWithBillDetailsAsync to avoid
    /// query-to-query injection.
    /// </summary>
    private async Task<IReadOnlyList<(ExpenseSplit Split, Expense Expense, Household Household)>> GetSplitsWithBillDetailsAsync(
        UserId userId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var splits = await _db.ExpenseSplits
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

        if (splits.Count == 0) return [];

        var expenseIds = splits.Select(s => s.ExpenseId).Distinct().ToList();

        var expenses = await _db.Expenses
            .AsNoTracking()
            .Where(b => expenseIds.Contains(b.Id) && b.IsActive && b.HouseholdId != null)
            .ToListAsync(cancellationToken);

        var relevantExpenses = expenses.Where(b =>
            b.RecurrenceSchedule == null
                ? b.DueDate >= from && b.DueDate <= to
                : b.RecurrenceSchedule.StartDate <= to &&
                  (b.RecurrenceSchedule.EndDate == null || b.RecurrenceSchedule.EndDate >= from)
        ).ToDictionary(b => b.Id);

        if (relevantExpenses.Count == 0) return [];

        var householdIds = relevantExpenses.Values.Select(b => b.HouseholdId!.Value).Distinct().ToList();
        var households = await _db.Households
            .AsNoTracking()
            .Where(h => householdIds.Contains(h.Id))
            .ToDictionaryAsync(h => h.Id, cancellationToken);

        return splits
            .Where(s => relevantExpenses.ContainsKey(s.ExpenseId))
            .Select(s =>
            {
                var b = relevantExpenses[s.ExpenseId];
                var h = households[b.HouseholdId!.Value];
                return (s, b, h);
            })
            .ToList();
    }

    private async Task<IReadOnlyDictionary<(Guid SplitId, DateTime OccurrenceDate), DateTime>> GetPaidSplitOccurrencesAsync(
        UserId userId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var splitIds = await _db.ExpenseSplits
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (splitIds.Count == 0) return new Dictionary<(Guid, DateTime), DateTime>();

        var payments = await _db.ExpenseSplitPayments
            .AsNoTracking()
            .Where(p => splitIds.Contains(p.ExpenseSplitId) && p.OccurrenceDate >= from && p.OccurrenceDate <= to)
            .Select(p => new { ExpenseSplitId = p.ExpenseSplitId.Value, p.OccurrenceDate, p.PaidAt })
            .ToListAsync(cancellationToken);

        return payments
            .GroupBy(p => (p.ExpenseSplitId, p.OccurrenceDate.Date))
            .ToDictionary(g => g.Key, g => g.Max(p => p.PaidAt));
    }

    private async Task<IReadOnlyDictionary<(Guid ExpenseId, DateTime OccurrenceDate), DateTime>> GetPaidPersonalBillOccurrencesAsync(
        UserId userId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var expenseIds = await _db.Expenses
            .AsNoTracking()
            .Where(b => b.UserId == userId && b.HouseholdId == null)
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);

        if (expenseIds.Count == 0) return new Dictionary<(Guid, DateTime), DateTime>();

        var payments = await _db.ExpensePayments
            .AsNoTracking()
            .Where(p => expenseIds.Contains(p.ExpenseId) && p.OccurrenceDate >= from && p.OccurrenceDate <= to)
            .Select(p => new { ExpenseId = p.ExpenseId.Value, p.OccurrenceDate, p.PaidAt })
            .ToListAsync(cancellationToken);

        return payments
            .GroupBy(p => (p.ExpenseId, p.OccurrenceDate.Date))
            .ToDictionary(g => g.Key, g => g.Max(p => p.PaidAt));
    }
}
