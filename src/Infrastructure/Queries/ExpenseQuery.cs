using Finance.Application.Dtos;
using Finance.Application.Queries;
using Finance.Application.Mappers;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class ExpenseQuery : IExpenseQuery
{
    private readonly FinanceDbContext _db;
    private readonly IExpenseSplitQuery _splitQuery;
    private readonly IHouseholdMembershipQuery _membershipQuery;

    public ExpenseQuery(FinanceDbContext db, IExpenseSplitQuery splitQuery, IHouseholdMembershipQuery membershipQuery)
    {
        _db = db;
        _splitQuery = splitQuery;
        _membershipQuery = membershipQuery;
    }

    // ── Personal expense queries ─────────────────────────────────────────────

    public async Task<ExpenseListDto> ListByUserAsync(ListExpensesParams request, CancellationToken cancellationToken = default)
    {
        var userId = UserId.Create(request.UserId);
        var query = _db.Expenses.Where(b => b.UserId == userId && b.HouseholdId == null);
        if (request.ActiveOnly) query = query.Where(b => b.IsActive);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(b => b.DueDate)
            .ThenBy(b => b.Title)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            return new ExpenseListDto([], total);

        var expenseIds = items.Select(b => b.Id).ToList();
        var occurrencesByExpenseId = items.ToDictionary(
            b => b.Id,
            b => OccurrenceDateComputer.ComputeCurrent(b.DueDate, b.RecurrenceSchedule));

        var payments = await _db.ExpensePayments
            .AsNoTracking()
            .Where(p => expenseIds.Contains(p.ExpenseId))
            .ToListAsync(cancellationToken);

        var paidExpenseIds = payments
            .Where(p => occurrencesByExpenseId.TryGetValue(p.ExpenseId, out var occ)
                        && p.OccurrenceDate.Date == occ.Date)
            .Select(p => p.ExpenseId)
            .ToHashSet();

        var responses = items
            .Select(b => ExpenseMapper.ToResponse(b, paidExpenseIds.Contains(b.Id)))
            .ToArray();

        return new ExpenseListDto(responses, total);
    }

    public async Task<IReadOnlyList<ExpenseDto>> GetAllActivePersonalByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var uid = UserId.Create(userId);
        var items = await _db.Expenses
            .AsNoTracking()
            .Where(e => e.UserId == uid && e.IsActive && e.HouseholdId == null)
            .OrderBy(e => e.DueDate)
            .ToListAsync(cancellationToken);
        return items.Select(i => ExpenseMapper.ToResponse(i)).ToList();
    }

        public async Task<ExpenseDto?> GetDetailAsync(ExpenseDetailParams request, CancellationToken cancellationToken = default)
    {
        var id = ExpenseId.Create(request.ExpenseId);
        var expense = await _db.Expenses.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && b.HouseholdId == null, cancellationToken);
        if (expense is null) return null;

        var occurrenceDate = OccurrenceDateComputer.ComputeCurrent(expense.DueDate, expense.RecurrenceSchedule);
        var payment = await _db.ExpensePayments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ExpenseId == id && p.OccurrenceDate == occurrenceDate, cancellationToken);

        return ExpenseMapper.ToResponse(expense, payment is not null);
    }

    public async Task<IReadOnlyDictionary<(Guid ExpenseId, DateTime OccurrenceDate), DateTime>> GetPaidOccurrencesAsync(
        UserId userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
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

    // ── Household expense queries ─────────────────────────────────────────────

    public async Task<HouseholdExpenseListDto> ListByHouseholdAsync(ListHouseholdExpensesParams request, CancellationToken cancellationToken = default)
    {
        var householdId = HouseholdId.Create(request.HouseholdId);
        var query = _db.Expenses.Where(b => b.HouseholdId == householdId);
        if (request.ActiveOnly) query = query.Where(b => b.IsActive);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(b => b.DueDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            return new HouseholdExpenseListDto([], total);

        HashSet<Guid> paidExpenseIds = [];
        if (request.CallerId.HasValue)
        {
            var callerUserId = UserId.Create(request.CallerId.Value);
            var expenseIds = items.Select(b => b.Id).ToList();
            var callerSplits = await _db.ExpenseSplits
            .AsNoTracking()
                .Where(s => s.UserId == callerUserId && expenseIds.Contains(s.ExpenseId))
                .ToListAsync(cancellationToken);

            if (callerSplits.Count > 0)
            {
                var splitIds = callerSplits.Select(s => s.Id).ToList();
                var payments = await _db.ExpenseSplitPayments
            .AsNoTracking()
                    .Where(p => splitIds.Contains(p.ExpenseSplitId))
                    .ToListAsync(cancellationToken);

                foreach (var expense in items)
                {
                    var occurrence = OccurrenceDateComputer.ComputeCurrent(expense.DueDate, expense.RecurrenceSchedule);
                    var split = callerSplits.FirstOrDefault(s => s.ExpenseId == expense.Id);
                    if (split is null) continue;

                    var isPaid = payments.Any(p => p.ExpenseSplitId == split.Id && p.OccurrenceDate.Date == occurrence.Date);
                    if (isPaid) paidExpenseIds.Add(expense.Id.Value);
                }
            }
        }

        var responses = items.Select(b =>
        {
            var occurrence = OccurrenceDateComputer.ComputeCurrent(b.DueDate, b.RecurrenceSchedule);
            var isPaid = paidExpenseIds.Contains(b.Id.Value);
            return ExpenseMapper.ToHouseholdResponse(b, isPaid) with { CurrentOccurrenceDate = occurrence };
        }).ToArray();

        return new HouseholdExpenseListDto(responses, total);
    }

    public async Task<HouseholdExpenseDto?> GetHouseholdDetailAsync(HouseholdExpenseDetailParams request, CancellationToken cancellationToken = default)
    {
        var id = ExpenseId.Create(request.ExpenseId);
        var expense = await _db.Expenses.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && b.HouseholdId != null, cancellationToken);
        return expense is null ? null : ExpenseMapper.ToHouseholdResponse(expense);
    }

    public async Task<IReadOnlyCollection<SplitDto>> ListSplitsAsync(ListSplitsParams request, CancellationToken cancellationToken = default)
    {
        var expenseId = ExpenseId.Create(request.ExpenseId);
        var splits = await _db.ExpenseSplits
            .AsNoTracking()
            .Where(s => s.ExpenseId == expenseId)
            .ToListAsync(cancellationToken);

        return splits.Select(ExpenseMapper.ToSplitResponse).ToArray();
    }

    public async Task<HouseholdExpenseDetailDto?> GetHouseholdExpenseDetailAsync(Guid expenseId, Guid callerId, CancellationToken cancellationToken = default)
    {
        var expense = await GetHouseholdDetailAsync(new HouseholdExpenseDetailParams(expenseId), cancellationToken);
        if (expense is null) return null;

        var splits = await ListSplitsAsync(new ListSplitsParams(expenseId), cancellationToken);
        var members = await _membershipQuery.ListMembersAsync(expense.HouseholdId, cancellationToken);

        var occurrenceDate = expense.CurrentOccurrenceDate == default ? expense.DueDate : expense.CurrentOccurrenceDate;
        var paidSplitIds = await _splitQuery.GetPaidSplitIdsForExpenseAsync(expenseId, occurrenceDate, cancellationToken);

        var memberDict = members.ToDictionary(m => m.MembershipId);
        var currentUserRole = members.FirstOrDefault(m => m.UserId == callerId)?.Role.ToString();

        var enrichedSplits = splits.Select(s =>
        {
            memberDict.TryGetValue(s.MembershipId, out var member);
            return new SplitDetailDto(
                s.SplitId,
                s.MembershipId,
                s.UserId,
                member?.DisplayName,
                null,
                member?.Role.ToString() ?? "Member",
                s.Amount,
                s.Currency,
                paidSplitIds.Contains(s.SplitId));
        }).ToList();

        return new HouseholdExpenseDetailDto(expense, enrichedSplits, members, currentUserRole);
    }
}
