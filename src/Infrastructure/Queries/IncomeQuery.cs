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
        var callerId = UserId.Create(request.CallerId);
        var income = await _db.IncomeSources.FirstOrDefaultAsync(
            i => i.Id == IncomeId.Create(request.IncomeId) && i.UserId == callerId, cancellationToken);
        return income is null ? null : IncomeMapper.ToResponse(income);
    }

    public async Task<NetPayBreakdownDto?> GetNetPayBreakdownAsync(GetNetPayBreakdownParams request, CancellationToken cancellationToken = default)
    {
        var income = await _incomeRepository.GetByIdAsync(IncomeId.Create(request.IncomeId), cancellationToken);
        if (income is null) return null;

        var breakdown = _deductionEngine.ComputeBreakdown(
            income.Id.Value,
            income.Amount.Amount,
            income.RecurrenceSchedule.Frequency,
            income.Amount.Currency,
            income.TaxProfile,
            income.Deductions,
            request.Year,
            request.Month);

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
            var breakdown = _deductionEngine.ComputeBreakdown(
                income.Id.Value,
                income.Amount.Amount,
                income.RecurrenceSchedule.Frequency,
                income.Amount.Currency,
                income.TaxProfile,
                income.Deductions,
                request.Year,
                request.Month);

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

        var personalCharges = await _db.Charges
            .AsNoTracking()
            .Where(e => e.UserId == uid && e.IsActive && e.GroupId == null)
            .OrderBy(e => e.DueDate)
            .ToListAsync(cancellationToken);

        var splits = await FetchAllocationsWithBillDetailsAsync(uid, windowStart, queryWindowEnd, cancellationToken);
        var paidAllocations = await FetchPaidAllocationOccurrencesAsync(uid, windowStart, queryWindowEnd, cancellationToken);
        var paidPersonal = await FetchPaidPersonalBillOccurrencesAsync(uid, windowStart, queryWindowEnd, cancellationToken);

        return _contributionCalculator.BuildSummaries(
            now, monthCount, pastMonths,
            incomeEntities, personalCharges,
            splits, paidAllocations, paidPersonal);
    }

    private async Task<IReadOnlyList<(Allocation Allocation, Charge Charge)>> FetchAllocationsWithBillDetailsAsync(
        UserId userId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var splits = await _db.Allocations
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

        if (splits.Count == 0) return [];

        var expenseIds = splits.Select(s => s.ChargeId).Distinct().ToList();

        var expenses = await _db.Charges
            .AsNoTracking()
            .Where(b => expenseIds.Contains(b.Id) && b.IsActive && b.GroupId != null)
            .ToListAsync(cancellationToken);

        var relevantCharges = expenses.Where(b =>
            b.RecurrenceSchedule == null
                ? b.DueDate >= from && b.DueDate <= to
                : b.RecurrenceSchedule.StartDate <= to &&
                  (b.RecurrenceSchedule.EndDate == null || b.RecurrenceSchedule.EndDate >= from)
        ).ToDictionary(b => b.Id);

        if (relevantCharges.Count == 0) return [];

        return splits
            .Where(s => relevantCharges.ContainsKey(s.ChargeId))
            .Select(s => (s, relevantCharges[s.ChargeId]))
            .ToList();
    }

    private async Task<IReadOnlyDictionary<(Guid AllocationId, DateTime OccurrenceDate), DateTime>> FetchPaidAllocationOccurrencesAsync(
        UserId userId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var splits = await _db.Allocations
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

        if (splits.Count == 0) return new Dictionary<(Guid, DateTime), DateTime>();

        var splitIds = splits.Select(s => s.Id.Value).ToList();
        var shareByAllocation = splits.ToDictionary(s => s.Id.Value, s => s.Amount.Amount);

        // An (allocation, occurrence) counts as paid when ledger settlements cover the share — a signed,
        // partial-aware sum. The representative timestamp is the latest value date, i.e. when the money
        // actually moved.
        var settledMap = await SettlementReads.GetSettledByAllocationOccurrenceAsync(
            _db, splitIds, cancellationToken);

        return settledMap
            .Where(kv => kv.Key.Occurrence >= from && kv.Key.Occurrence <= to
                      && kv.Value.Settled >= (shareByAllocation.TryGetValue(kv.Key.AllocationId, out var share) ? share : 0m))
            .ToDictionary(kv => kv.Key, kv => kv.Value.LatestValueDate);
    }

    private async Task<IReadOnlyDictionary<(Guid ChargeId, DateTime OccurrenceDate), DateTime>> FetchPaidPersonalBillOccurrencesAsync(
        UserId userId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var expenseIds = await _db.Charges
            .AsNoTracking()
            .Where(b => b.UserId == userId && b.GroupId == null)
            .Select(b => b.Id)
            .ToListAsync(cancellationToken);

        if (expenseIds.Count == 0) return new Dictionary<(Guid, DateTime), DateTime>();

        var payments = await _db.ChargePayments
            .AsNoTracking()
            .Where(p => expenseIds.Contains(p.ChargeId) && p.OccurrenceDate >= from && p.OccurrenceDate <= to)
            .Select(p => new { ChargeId = p.ChargeId.Value, p.OccurrenceDate, p.PaidAt })
            .ToListAsync(cancellationToken);

        return payments
            .GroupBy(p => (p.ChargeId, p.OccurrenceDate.Date))
            .ToDictionary(g => g.Key, g => g.Max(p => p.PaidAt));
    }
}
