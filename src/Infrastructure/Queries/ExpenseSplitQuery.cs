using Finance.Application.Dtos;
using Finance.Application.Queries;
using Finance.Domain.Aggregates;
using Finance.Domain.ReadModels;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class ExpenseSplitQuery : IExpenseSplitQuery
{
    private readonly FinanceDbContext _dbContext;

    public ExpenseSplitQuery(FinanceDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyCollection<SplitWithSharedExpenseDetail>> ListByUserWithBillDetailsAsync(
        UserId userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var splits = await _dbContext.ExpenseSplits
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

        if (splits.Count == 0) return [];

        var expenseIds = splits.Select(s => s.ExpenseId).Distinct().ToList();

        var expenses = await _dbContext.Expenses
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

        var households = await _dbContext.Households
            .AsNoTracking()
            .Where(h => householdIds.Contains(h.Id))
            .ToDictionaryAsync(h => h.Id, cancellationToken);

        var relevantSplitIds = splits
            .Where(s => relevantExpenses.ContainsKey(s.ExpenseId))
            .Select(s => s.Id)
            .ToList();

        var payments = await _dbContext.ExpenseSplitPayments
            .AsNoTracking()
            .Where(p => relevantSplitIds.Contains(p.ExpenseSplitId))
            .ToListAsync(cancellationToken);

        var paymentLookup = payments
            .Select(p => (p.ExpenseSplitId, p.OccurrenceDate.Date))
            .ToHashSet();
        var paidAtLookup = payments.ToDictionary(p => (p.ExpenseSplitId, p.OccurrenceDate.Date), p => p.PaidAt);

        return splits
            .Where(s => relevantExpenses.ContainsKey(s.ExpenseId))
            .Select(s =>
            {
                var b = relevantExpenses[s.ExpenseId];
                var h = households[b.HouseholdId!.Value];
                var occDate = OccurrenceDateComputer.ComputeCurrent(b.DueDate, b.RecurrenceSchedule);
                var isClaimed = paymentLookup.Contains((s.Id, occDate.Date));
                return new SplitWithSharedExpenseDetail(
                    s.Id.Value, b.Id.Value, h.Id.Value, h.Name,
                    b.Title, b.Category.ToString(),
                    s.Amount.Amount, s.Amount.Currency,
                    b.DueDate, isClaimed,
                    isClaimed ? paidAtLookup.GetValueOrDefault((s.Id, occDate.Date)) : null,
                    null,
                    b.RecurrenceSchedule?.Frequency,
                    b.RecurrenceSchedule?.StartDate,
                    b.RecurrenceSchedule?.EndDate);
            })
            .ToList();
    }

    public async Task<IReadOnlyCollection<HouseholdMonthlyContributionsDto>> ListByHouseholdAsync(
        HouseholdId householdId, DateTime windowStart, DateTime windowEnd, CancellationToken cancellationToken = default)
    {
        var allExpenses = await _dbContext.Expenses
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

        var splits = await _dbContext.ExpenseSplits
            .AsNoTracking()
            .Where(s => expenseIds.Contains(s.ExpenseId))
            .ToListAsync(cancellationToken);

        if (splits.Count == 0) return BuildEmptyMonths(windowStart, windowEnd);

        var expenseById = relevantExpenses.ToDictionary(b => b.Id);
        var splitUserIds = splits.Select(s => s.UserId).Distinct().ToList();

        var userProjections = await _dbContext.UserProjections
            .AsNoTracking()
            .Where(p => splitUserIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);

        var nameById = userProjections.ToDictionary(p => p.UserId.Value, p => p.GetFullName());

        var splitIds = splits.Select(s => s.Id).ToList();

        var splitPayments = await _dbContext.ExpenseSplitPayments
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

    public async Task<IReadOnlyDictionary<Guid, CallerSplitStatusDto>> GetCallerPaymentStatusAsync(
        UserId callerId,
        IReadOnlyCollection<(Guid ExpenseId, DateTime OccurrenceDate)> expenseOccurrences,
        CancellationToken cancellationToken = default)
    {
        if (expenseOccurrences.Count == 0) return new Dictionary<Guid, CallerSplitStatusDto>();

        var expenseIds = expenseOccurrences.Select(x => ExpenseId.Create(x.ExpenseId)).ToList();

        var splits = await _dbContext.ExpenseSplits
            .AsNoTracking()
            .Where(s => s.UserId == callerId && expenseIds.Contains(s.ExpenseId))
            .ToListAsync(cancellationToken);

        if (splits.Count == 0) return new Dictionary<Guid, CallerSplitStatusDto>();

        var splitByExpenseId = splits.ToDictionary(s => s.ExpenseId.Value);
        var splitIds = splits.Select(s => s.Id).ToList();

        var payments = await _dbContext.ExpenseSplitPayments
            .AsNoTracking()
            .Where(p => splitIds.Contains(p.ExpenseSplitId))
            .ToListAsync(cancellationToken);

        var result = new Dictionary<Guid, CallerSplitStatusDto>();

        foreach (var (expenseId, occurrenceDate) in expenseOccurrences)
        {
            if (!splitByExpenseId.TryGetValue(expenseId, out var split)) continue;
            var occ = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc);
            var isPaid = payments.Any(p => p.ExpenseSplitId == split.Id && p.OccurrenceDate.Date == occ.Date);
            result[expenseId] = new CallerSplitStatusDto(split.Id.Value, isPaid);
        }

        return result;
    }

    public async Task<IReadOnlySet<Guid>> GetPaidSplitIdsForExpenseAsync(
        Guid expenseId, DateTime occurrenceDate, CancellationToken cancellationToken = default)
    {
        var occ = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc);
        var eid = ExpenseId.Create(expenseId);

        var splitIds = await _dbContext.ExpenseSplits
            .AsNoTracking()
            .Where(s => s.ExpenseId == eid)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var paidSplitIds = await _dbContext.ExpenseSplitPayments
            .AsNoTracking()
            .Where(p => splitIds.Contains(p.ExpenseSplitId) && p.OccurrenceDate == occ)
            .Select(p => p.ExpenseSplitId.Value)
            .ToListAsync(cancellationToken);

        return paidSplitIds.ToHashSet();
    }

    public async Task<IReadOnlyDictionary<(Guid SplitId, DateTime OccurrenceDate), DateTime>> GetPaidOccurrencesAsync(
        UserId userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var splitIds = await _dbContext.ExpenseSplits
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        if (splitIds.Count == 0) return new Dictionary<(Guid, DateTime), DateTime>();

        var payments = await _dbContext.ExpenseSplitPayments
            .AsNoTracking()
            .Where(p => splitIds.Contains(p.ExpenseSplitId) && p.OccurrenceDate >= from && p.OccurrenceDate <= to)
            .Select(p => new { ExpenseSplitId = p.ExpenseSplitId.Value, p.OccurrenceDate, p.PaidAt })
            .ToListAsync(cancellationToken);

        return payments
            .GroupBy(p => (p.ExpenseSplitId, p.OccurrenceDate.Date))
            .ToDictionary(g => g.Key, g => g.Max(p => p.PaidAt));
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

