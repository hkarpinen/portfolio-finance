using Finance.Application.Dtos;
using Finance.Application.Queries;
using Finance.Application.Mappers;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class ExpenseQuery : IExpenseQuery
{
    private readonly FinanceDbContext _db;

    public ExpenseQuery(FinanceDbContext db) => _db = db;

    // ── Personal expense queries ──────────────────────────────────────────────

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
            b => b.RecurrenceSchedule?.CurrentOccurrence(b.DueDate) ?? b.DueDate);

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

    public async Task<ExpenseDto?> GetDetailAsync(ExpenseDetailParams request, CancellationToken cancellationToken = default)
    {
        var id = ExpenseId.Create(request.ExpenseId);
        var expense = await _db.Expenses.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && b.HouseholdId == null, cancellationToken);
        if (expense is null) return null;

        var occurrenceDate = expense.RecurrenceSchedule?.CurrentOccurrence(expense.DueDate) ?? expense.DueDate;
        var payment = await _db.ExpensePayments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ExpenseId == id && p.OccurrenceDate == occurrenceDate, cancellationToken);

        return ExpenseMapper.ToResponse(expense, payment is not null);
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
                    var occurrence = expense.RecurrenceSchedule?.CurrentOccurrence(expense.DueDate) ?? expense.DueDate;
                    var split = callerSplits.FirstOrDefault(s => s.ExpenseId == expense.Id);
                    if (split is null) continue;

                    var isPaid = payments.Any(p => p.ExpenseSplitId == split.Id && p.OccurrenceDate.Date == occurrence.Date);
                    if (isPaid) paidExpenseIds.Add(expense.Id.Value);
                }
            }
        }

        var responses = items.Select(b =>
        {
            var occurrence = b.RecurrenceSchedule?.CurrentOccurrence(b.DueDate) ?? b.DueDate;
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

        // Inline membership lookup (no circular IHouseholdQuery dep)
        var hid = HouseholdId.Create(expense.HouseholdId);
        var memberEntities = await _db.HouseholdMemberships
            .AsNoTracking()
            .Where(m => m.HouseholdId == hid && m.IsActive)
            .ToListAsync(cancellationToken);
        var memberUserIds = memberEntities.Select(m => m.UserId).ToList();
        var memberProjections = await _db.UserProjections
            .AsNoTracking()
            .Where(p => memberUserIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);
        var memberProjDict = memberProjections.ToDictionary(p => p.UserId);
        var members = memberEntities.Select(m =>
        {
            memberProjDict.TryGetValue(m.UserId, out var proj);
            return MembershipMapper.ToResponse(m, proj?.GetFullName());
        }).ToList();

        var occurrenceDate = expense.CurrentOccurrenceDate == default ? expense.DueDate : expense.CurrentOccurrenceDate;
        var paidSplitIds = await GetPaidSplitIdsForExpenseAsync(expenseId, occurrenceDate, cancellationToken);

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

    public Task<bool> ExistsForUserAsync(UserId userId, string title, decimal amount, CancellationToken cancellationToken = default)
        => _db.Expenses.AsNoTracking()
            .AnyAsync(
                e => e.UserId == userId && e.IsActive && e.Title == title && e.Amount.Amount == amount,
                cancellationToken);

    // ── Expense-split queries ─────────────────────────────────────────────────

    public async Task<IReadOnlyCollection<HouseholdMonthlyContributionsDto>> ListSplitsByHouseholdAsync(
        HouseholdId householdId, DateTime windowStart, DateTime windowEnd, CancellationToken cancellationToken = default)
    {
        var allExpenses = await _db.Expenses
            .AsNoTracking()
            .Where(b => b.HouseholdId == householdId && b.IsActive)
            .ToListAsync(cancellationToken);

        var relevantExpenses = allExpenses.Where(b =>
            b.RecurrenceSchedule == null
                ? b.DueDate >= windowStart && b.DueDate <= windowEnd
                : b.RecurrenceSchedule.StartDate <= windowEnd &&
                  (b.RecurrenceSchedule.EndDate == null || b.RecurrenceSchedule.EndDate >= windowStart)
        ).ToList();

        if (relevantExpenses.Count == 0) return BuildEmptyMonths(windowStart, windowEnd);

        var expenseIds = relevantExpenses.Select(b => b.Id).ToList();

        var splits = await _db.ExpenseSplits
            .AsNoTracking()
            .Where(s => expenseIds.Contains(s.ExpenseId))
            .ToListAsync(cancellationToken);

        if (splits.Count == 0) return BuildEmptyMonths(windowStart, windowEnd);

        var expenseById = relevantExpenses.ToDictionary(b => b.Id);
        var splitUserIds = splits.Select(s => s.UserId).Distinct().ToList();

        var userProjections = await _db.UserProjections
            .AsNoTracking()
            .Where(p => splitUserIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);

        var nameById = userProjections.ToDictionary(p => p.UserId.Value, p => p.GetFullName());

        var splitIds = splits.Select(s => s.Id).ToList();

        var splitPayments = await _db.ExpenseSplitPayments
            .AsNoTracking()
            .Where(p => splitIds.Contains(p.ExpenseSplitId))
            .ToListAsync(cancellationToken);

        var paymentSet = splitPayments
            .Select(p => (p.ExpenseSplitId, p.OccurrenceDate.Date))
            .ToHashSet();
        var windowEndExclusive = windowEnd.AddDays(1);

        var projected = new List<(int Year, int Month, Guid UserId, bool IsClaimed, decimal Amount, string Currency, HouseholdContributionItemDto Item)>();

        foreach (var split in splits)
        {
            if (!expenseById.TryGetValue(split.ExpenseId, out var expense)) continue;

            IEnumerable<DateTime> occurrenceDates = expense.RecurrenceSchedule != null
                ? expense.RecurrenceSchedule.GetOccurrencesInRange(windowStart, windowEndExclusive)
                : [expense.DueDate];

            foreach (var date in occurrenceDates)
            {
                var isClaimed = paymentSet.Contains((split.Id, date.Date));
                projected.Add((date.Year, date.Month, split.UserId.Value, isClaimed,
                    split.Amount.Amount, split.Amount.Currency,
                    new HouseholdContributionItemDto(
                        split.Id.Value, expense.Id.Value,
                        expense.Title, expense.Category.ToString(),
                        split.Amount.Amount, split.Amount.Currency,
                        date, isClaimed)));
            }
        }

        var monthCount = ((windowEnd.Year * 12 + windowEnd.Month) - (windowStart.Year * 12 + windowStart.Month)) + 1;
        var result = new List<HouseholdMonthlyContributionsDto>(monthCount);

        for (var m = 0; m < monthCount; m++)
        {
            var mStart = windowStart.AddMonths(m);
            var label = mStart.ToString("MMMM yyyy");
            var currency = "USD";

            var monthItems = projected
                .Where(p => p.Year == mStart.Year && p.Month == mStart.Month)
                .ToList();

            var byMember = monthItems
                .GroupBy(p => p.UserId)
                .Select(g =>
                {
                    var contributions = g.Select(p => p.Item).OrderBy(i => i.DueDate).ToList();
                    var totalDue = g.Sum(p => p.Amount);
                    var totalPaid = g.Where(p => p.IsClaimed).Sum(p => p.Amount);
                    if (contributions.Count > 0) currency = contributions[0].Currency;
                    nameById.TryGetValue(g.Key, out var displayName);
                    return new HouseholdMemberContributionDto(g.Key, displayName, totalDue, totalPaid, contributions);
                })
                .OrderBy(m2 => m2.UserId)
                .ToList();

            var total = byMember.Sum(m2 => m2.TotalDue);
            result.Add(new HouseholdMonthlyContributionsDto(label, mStart, total, currency, byMember));
        }

        return result;
    }

    private async Task<IReadOnlySet<Guid>> GetPaidSplitIdsForExpenseAsync(
        Guid expenseId, DateTime occurrenceDate, CancellationToken cancellationToken = default)
    {
        var occ = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc);
        var eid = ExpenseId.Create(expenseId);

        var splitIds = await _db.ExpenseSplits
            .AsNoTracking()
            .Where(s => s.ExpenseId == eid)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var paidSplitIds = await _db.ExpenseSplitPayments
            .AsNoTracking()
            .Where(p => splitIds.Contains(p.ExpenseSplitId) && p.OccurrenceDate == occ)
            .Select(p => p.ExpenseSplitId.Value)
            .ToListAsync(cancellationToken);

        return paidSplitIds.ToHashSet();
    }

    private static IReadOnlyCollection<HouseholdMonthlyContributionsDto> BuildEmptyMonths(DateTime windowStart, DateTime windowEnd)
    {
        var monthCount = ((windowEnd.Year * 12 + windowEnd.Month) - (windowStart.Year * 12 + windowStart.Month)) + 1;
        return Enumerable.Range(0, monthCount)
            .Select(m =>
            {
                var mStart = windowStart.AddMonths(m);
                return new HouseholdMonthlyContributionsDto(mStart.ToString("MMMM yyyy"), mStart, 0, "USD", []);
            })
            .ToList();
    }
}
