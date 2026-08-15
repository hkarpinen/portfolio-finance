using Finance.Application.Dtos;
using Finance.Application.Queries;
using Finance.Application.Ports;
using Finance.Application.Repositories;
using Finance.Application.Mappers;
using Finance.Application.Utilities;
using Finance.Domain.Aggregates;
using Finance.Domain.Engines;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class IncomeQuery : IIncomeQuery
{
    private readonly FinanceDbContext _db;
    private readonly IIncomeSourceRepository _incomeRepository;
    private readonly IContributionCalculator _contributionCalculator;
    private readonly IPayrollDeductionEngine _deductionEngine;

    public IncomeQuery(
        FinanceDbContext db,
        IIncomeSourceRepository incomeRepository,
        IContributionCalculator contributionCalculator,
        IPayrollDeductionEngine deductionEngine)
    {
        _db = db;
        _incomeRepository = incomeRepository;
        _contributionCalculator = contributionCalculator;
        _deductionEngine = deductionEngine;
    }

    public async Task<IncomeListDto> ListAsync(ListIncomeParams request, CancellationToken cancellationToken = default)
    {
        var uid = UserId.Create(request.UserId);
        var query = _db.IncomeSources.AsNoTracking().Where(i => i.UserId == uid);
        if (request.ActiveOnly) query = query.Where(i => i.IsActive);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(i => i.Source)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new IncomeListDto(items.Select(IncomeMapper.ToResponse).ToArray(), total);
    }

    public async Task<IncomeListDto> ListByUserAsync(ListUserIncomeParams request, CancellationToken cancellationToken = default)
    {
        var query = _db.IncomeSources.AsNoTracking().Where(i => i.UserId == UserId.Create(request.UserId));
        if (request.ActiveOnly) query = query.Where(i => i.IsActive);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(i => i.Source)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new IncomeListDto(items.Select(IncomeMapper.ToResponse).ToArray(), total);
    }

    public async Task<IncomeDto?> GetDetailAsync(IncomeDetailParams request, CancellationToken cancellationToken = default)
    {
        // Owner-scoped: null (→ 404) for anyone else, so an id cannot be probed.
        var callerUserId = UserId.Create(request.CallerUserId);
        var income = await _db.IncomeSources.FirstOrDefaultAsync(
            i => i.Id == IncomeId.Create(request.IncomeId) && i.UserId == callerUserId, cancellationToken);
        return income is null ? null : IncomeMapper.ToResponse(income);
    }

    public async Task<NetPayBreakdownDto?> GetNetPayBreakdownAsync(GetNetPayBreakdownParams request, CancellationToken cancellationToken = default)
    {
        var income = await _incomeRepository.GetByIdAsync(IncomeId.Create(request.IncomeId), cancellationToken);
        if (income is null) return null;
        if (income.UserId.Value != request.CallerUserId) return null;

        var breakdown = _deductionEngine.ComputeBreakdown(income, request.Year, request.Month);

        return new NetPayBreakdownDto(
            breakdown.IncomeId,
            breakdown.GrossPay,
            breakdown.Currency,
            breakdown.Deductions
                .Select(d => new DeductionLineItemDto(d.Type, d.Label, d.IsEmployerSponsored, d.Amount, d.Currency))
                .ToList().AsReadOnly(),
            breakdown.TotalDeductions,
            breakdown.NetPay);
    }

    public async Task<NetPaySummaryDto> GetNetPaySummaryAsync(GetNetPaySummaryParams request, CancellationToken cancellationToken = default)
    {
        var uid = UserId.Create(request.UserId);
        var sources = await _db.IncomeSources
            .AsNoTracking()
            .Where(i => i.UserId == uid && i.IsActive)
            .ToListAsync(cancellationToken);

        if (sources.Count == 0)
        {
            return new NetPaySummaryDto(request.Year, request.Month, "USD",
                MonthlyGross: 0m, MonthlyNet: 0m, TotalTaxWithheld: 0m,
                TotalDeductions: 0m, AnnualGross: 0m, SourceCount: 0);
        }

        decimal monthlyGross = 0m, monthlyNet = 0m, totalDeductions = 0m, totalTax = 0m;
        // Gross is tracked per currency so the summary can advertise the dominant one. Amounts in other
        // currencies are still summed raw — there is no FX conversion anywhere in this path.
        var grossByCurrency = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var income in sources)
        {
            var breakdown = _deductionEngine.ComputeBreakdown(income, request.Year, request.Month);

            monthlyGross += breakdown.GrossPay;
            monthlyNet += breakdown.NetPay;
            totalDeductions += breakdown.TotalDeductions;
            foreach (var d in breakdown.Deductions)
            {
                if (IsTaxDeduction(d.Type)) totalTax += d.Amount;
            }

            grossByCurrency.TryGetValue(breakdown.Currency, out var sofar);
            grossByCurrency[breakdown.Currency] = sofar + breakdown.GrossPay;
        }

        var dominantCurrency = grossByCurrency
            .OrderByDescending(kv => kv.Value)
            .Select(kv => kv.Key)
            .FirstOrDefault() ?? "USD";

        return new NetPaySummaryDto(
            request.Year, request.Month, dominantCurrency,
            MonthlyGross: monthlyGross,
            MonthlyNet: monthlyNet,
            TotalTaxWithheld: totalTax,
            TotalDeductions: totalDeductions,
            AnnualGross: monthlyGross * 12,
            SourceCount: sources.Count);
    }

    // These strings must match the labels the payroll-deduction engine emits verbatim — the two sides
    // are coupled by string value only, so a renamed label silently reclassifies a tax as a benefit.
    private static bool IsTaxDeduction(string type) => type switch
    {
        "FederalIncomeTax" or "StateIncomeTax" or "SocialSecurity" or "Medicare" => true,
        _ => false,
    };

    public Task<bool> ExistsForUserAsync(UserId userId, string source, decimal amount, CancellationToken cancellationToken = default)
        => _db.IncomeSources.AsNoTracking()
            .AnyAsync(
                i => i.UserId == userId && i.IsActive && i.Source == source && i.Amount.Amount == amount,
                cancellationToken);

    public async Task<IReadOnlyCollection<ContributionPeriodSummaryDto>> GetContributionSummariesAsync(
        Guid userId,
        DateTime now,
        int monthCount,
        int pastMonths,
        CancellationToken cancellationToken = default)
    {
        var uid = UserId.Create(userId);
        var windowStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-pastMonths);
        var queryWindowEnd = windowStart.AddMonths(monthCount).AddDays(-1);

        var incomeEntities = await _db.IncomeSources
            .AsNoTracking()
            .Where(i => i.UserId == uid && i.IsActive)
            .OrderBy(i => i.Source)
            .ToListAsync(cancellationToken);

        var personalExpenses = await _db.Expenses
            .AsNoTracking()
            .Where(e => e.Owner.Kind == EntityKind.Person && e.Owner.Id == uid.Value && e.IsActive)
            .OrderBy(e => e.DueDate)
            .ToListAsync(cancellationToken);

        var splits = await FetchSharesWithBillDetailsAsync(uid, windowStart, queryWindowEnd, cancellationToken);
        var paidShares = await FetchPaidShareOccurrencesAsync(uid, windowStart, queryWindowEnd, cancellationToken);
        var paidPersonal = await FetchPaidPersonalBillOccurrencesAsync(uid, windowStart, queryWindowEnd, cancellationToken);

        return _contributionCalculator.BuildSummaries(
            now, monthCount, pastMonths,
            incomeEntities, personalExpenses,
            splits, paidShares, paidPersonal);
    }

    private async Task<IReadOnlyList<(Share Share, Expense Expense)>> FetchSharesWithBillDetailsAsync(
        UserId userId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var splits = await _db.Shares
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

        if (splits.Count == 0) return [];

        var expenseIds = splits.Select(s => s.ExpenseId).Distinct().ToList();

        // One expense is one occurrence, so the window is a plain date filter the database can run.
        var relevantExpenses = (await _db.Expenses
            .AsNoTracking()
            .Where(b => expenseIds.Contains(b.Id) && b.IsActive && b.Owner.Kind == EntityKind.Group
                        && b.OccurrenceDate >= from && b.OccurrenceDate <= to)
            .ToListAsync(cancellationToken))
            .ToDictionary(b => b.Id);

        if (relevantExpenses.Count == 0) return [];

        return splits
            .Where(s => relevantExpenses.ContainsKey(s.ExpenseId))
            .Select(s => (s, relevantExpenses[s.ExpenseId]))
            .ToList();
    }

    private async Task<IReadOnlyDictionary<(Guid ShareId, DateTime OccurrenceDate), DateTime>> FetchPaidShareOccurrencesAsync(
        UserId userId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var splits = await _db.Shares
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

        if (splits.Count == 0) return new Dictionary<(Guid, DateTime), DateTime>();

        var splitIds = splits.Select(s => s.Id.Value).ToList();
        var shareByShare = splits.ToDictionary(s => s.Id.Value, s => s.Amount.Amount);

        // An (share, occurrence) counts as paid when ledger settlements cover the share — a signed,
        // partial-aware sum. The representative timestamp is the latest value date, i.e. when the money
        // actually moved.
        var settledMap = await SettlementReads.GetSettledByShareOccurrenceAsync(
            _db, splitIds, cancellationToken);

        return settledMap
            .Where(kv => kv.Key.Occurrence >= from && kv.Key.Occurrence <= to
                      && kv.Value.Settled >= (shareByShare.TryGetValue(kv.Key.ShareId, out var share) ? share : 0m))
            .ToDictionary(kv => kv.Key, kv => kv.Value.LatestValueDate);
    }

    private async Task<IReadOnlyDictionary<(Guid ExpenseId, DateTime OccurrenceDate), DateTime>> FetchPaidPersonalBillOccurrencesAsync(
        UserId userId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        // Id and occurrence together — the occurrence is what the answer is keyed on, and fetching
        // it here is what lets the entries be matched without a join.
        var expenses = await _db.Expenses
            .AsNoTracking()
            .Where(b => b.Owner.Kind == EntityKind.Person && b.Owner.Id == userId.Value)
            .Select(b => new { b.Id, b.OccurrenceDate })
            .ToListAsync(cancellationToken);

        if (expenses.Count == 0) return new Dictionary<(Guid, DateTime), DateTime>();

        // Raw Guids: SourceExpenseId is a plain nullable Guid column, while Expense.Id is
        // value-converted. Joining the two in the database meant reaching through that conversion
        // with `c.Id.Value`, which EF cannot translate — so the ids come back first and the match
        // happens here, over a list this caller already had to load anyway.
        var ids = expenses.Select(b => b.Id.Value).ToList();
        var occurrenceOf = expenses.ToDictionary(b => b.Id.Value, b => b.OccurrenceDate);

        // When a personal expense was paid IS when it was booked: it posts on the day it belongs
        // to, so the entry's own date answers this without a second record of the same fact.
        var entries = await _db.JournalEntries.AsNoTracking()
            .Where(e => e.ReversalOfEntryId == null && e.ReversedByEntryId == null
                        && e.Date >= from && e.Date <= to
                        && e.SourceExpenseId != null && ids.Contains(e.SourceExpenseId.Value))
            .Select(e => new { e.SourceExpenseId, e.RecordedAt })
            .ToListAsync(cancellationToken);

        return entries
            .GroupBy(e => (ExpenseId: e.SourceExpenseId!.Value, occurrenceOf[e.SourceExpenseId!.Value].Date))
            .ToDictionary(g => g.Key, g => g.Max(e => e.RecordedAt));
    }
}
