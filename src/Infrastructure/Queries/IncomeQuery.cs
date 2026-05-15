using Finance.Application.Dtos;
using Finance.Application.Queries;
using Finance.Application.Ports;
using Finance.Application.Repositories;
using Finance.Application.Mappers;
using Finance.Application.Utilities;
using Finance.Domain.Aggregates;
using Finance.Domain.Engines;
using Finance.Domain.Utilities;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class IncomeQuery : IIncomeQuery
{
    private readonly FinanceDbContext _db;
    private readonly IIncomeSourceRepository _incomeRepository;

    public IncomeQuery(
        FinanceDbContext db,
        IIncomeSourceRepository incomeRepository
)
    {
        _db = db;
        _incomeRepository = incomeRepository;
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
        var income = await _db.IncomeSources.FirstOrDefaultAsync(i => i.Id == IncomeId.Create(request.IncomeId), cancellationToken);
        return income is null ? null : IncomeMapper.ToResponse(income);
    }

    public async Task<NetPayBreakdownDto?> GetNetPayBreakdownAsync(GetNetPayBreakdownParams request, CancellationToken cancellationToken = default)
    {
        var income = await _incomeRepository.GetByIdAsync(IncomeId.Create(request.IncomeId), cancellationToken);
        if (income is null) return null;

        var breakdown = PayrollDeductionEngine.ComputeBreakdown(
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

    public Task<bool> ExistsForUserAsync(UserId userId, string source, decimal amount, CancellationToken cancellationToken = default)
        => _db.IncomeSources.AsNoTracking()
            .AnyAsync(
                i => i.UserId == userId && i.IsActive && i.Source == source && i.Amount.Amount == amount,
                cancellationToken);

    // ── Contribution / budget timeline ────────────────────────────────────────

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
            .Where(e => e.UserId == uid && e.IsActive && e.GroupId == null)
            .OrderBy(e => e.DueDate)
            .ToListAsync(cancellationToken);

        var splits = await FetchSplitsWithBillDetailsAsync(uid, windowStart, queryWindowEnd, cancellationToken);
        var paidSplits = await FetchPaidSplitOccurrencesAsync(uid, windowStart, queryWindowEnd, cancellationToken);
        var paidPersonal = await FetchPaidPersonalBillOccurrencesAsync(uid, windowStart, queryWindowEnd, cancellationToken);

        return ContributionCalculator.BuildSummaries(
            now, monthCount, pastMonths,
            incomeEntities, personalExpenses,
            splits, paidSplits, paidPersonal);
    }

    // ── Private DB fetch helpers ──────────────────────────────────────────────

    private async Task<IReadOnlyList<(ExpenseSplit Split, Expense Expense)>> FetchSplitsWithBillDetailsAsync(
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
            .Where(b => expenseIds.Contains(b.Id) && b.IsActive && b.GroupId != null)
            .ToListAsync(cancellationToken);

        var relevantExpenses = expenses.Where(b =>
            b.RecurrenceSchedule == null
                ? b.DueDate >= from && b.DueDate <= to
                : b.RecurrenceSchedule.StartDate <= to &&
                  (b.RecurrenceSchedule.EndDate == null || b.RecurrenceSchedule.EndDate >= from)
        ).ToDictionary(b => b.Id);

        if (relevantExpenses.Count == 0) return [];

        return splits
            .Where(s => relevantExpenses.ContainsKey(s.ExpenseId))
            .Select(s => (s, relevantExpenses[s.ExpenseId]))
            .ToList();
    }

    private async Task<IReadOnlyDictionary<(Guid SplitId, DateTime OccurrenceDate), DateTime>> FetchPaidSplitOccurrencesAsync(
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

    private async Task<IReadOnlyDictionary<(Guid ExpenseId, DateTime OccurrenceDate), DateTime>> FetchPaidPersonalBillOccurrencesAsync(
        UserId userId, DateTime from, DateTime to, CancellationToken cancellationToken)
    {
        var expenseIds = await _db.Expenses
            .AsNoTracking()
            .Where(b => b.UserId == userId && b.GroupId == null)
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
