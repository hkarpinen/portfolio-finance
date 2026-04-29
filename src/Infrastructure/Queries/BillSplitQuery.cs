using Finance.Application.Contracts;
using Finance.Application.Queries;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class BillSplitQuery : IBillSplitQuery
{
    private readonly FinanceDbContext _dbContext;

    public BillSplitQuery(FinanceDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyCollection<SplitWithBillDetail>> ListByUserWithBillDetailsAsync(
        UserId userId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        var splits = await _dbContext.BillSplits
            .Where(s => s.UserId == userId)
            .ToListAsync(cancellationToken);

        if (splits.Count == 0) return [];

        var billIds = splits.Select(s => s.BillId).Distinct().ToList();
        var bills = await _dbContext.Bills
            .Where(b => billIds.Contains(b.Id) && b.IsActive)
            .ToListAsync(cancellationToken);

        var relevantBills = bills.Where(b =>
            b.RecurrenceSchedule == null
                ? b.DueDate >= from && b.DueDate <= to
                : b.RecurrenceSchedule.StartDate <= to &&
                  (b.RecurrenceSchedule.EndDate == null || b.RecurrenceSchedule.EndDate >= from)
        ).ToDictionary(b => b.Id);

        if (relevantBills.Count == 0) return [];

        var householdIds = relevantBills.Values.Select(b => b.HouseholdId).Distinct().ToList();
        var households = await _dbContext.Households
            .Where(h => householdIds.Contains(h.Id))
            .ToDictionaryAsync(h => h.Id, cancellationToken);

        return splits
            .Where(s => relevantBills.ContainsKey(s.BillId))
            .Select(s =>
            {
                var b = relevantBills[s.BillId];
                var h = households[b.HouseholdId];
                return new SplitWithBillDetail(
                    s.Id.Value, b.Id.Value, h.Id.Value, h.Name,
                    b.Title, b.Category.ToString(),
                    s.Amount.Amount, s.Amount.Currency,
                    b.DueDate, s.IsClaimed, s.ClaimedAt, null,
                    b.RecurrenceSchedule?.Frequency,
                    b.RecurrenceSchedule?.StartDate,
                    b.RecurrenceSchedule?.EndDate);
            })
            .ToList();
    }

    public async Task<IReadOnlyCollection<HouseholdMonthlyContributions>> ListByHouseholdAsync(
        HouseholdId householdId, DateTime windowStart, DateTime windowEnd, CancellationToken cancellationToken = default)
    {
        // Load all active bills for the household whose schedule overlaps the window
        var bills = await _dbContext.Bills
            .Where(b => b.HouseholdId == householdId && b.IsActive)
            .ToListAsync(cancellationToken);

        var relevantBills = bills.Where(b =>
            b.RecurrenceSchedule == null
                ? b.DueDate >= windowStart && b.DueDate <= windowEnd
                : b.RecurrenceSchedule.StartDate <= windowEnd &&
                  (b.RecurrenceSchedule.EndDate == null || b.RecurrenceSchedule.EndDate >= windowStart)
        ).ToList();

        if (relevantBills.Count == 0) return BuildEmptyMonths(windowStart, windowEnd);

        var billIds = relevantBills.Select(b => b.Id).ToList();
        var splits = await _dbContext.BillSplits
            .Where(s => billIds.Contains(s.BillId))
            .ToListAsync(cancellationToken);

        if (splits.Count == 0) return BuildEmptyMonths(windowStart, windowEnd);

        var billById = relevantBills.ToDictionary(b => b.Id);

        // Resolve display names for all users who have splits in this household
        var splitUserIds = splits.Select(s => s.UserId).Distinct().ToList();
        var userProjections = await _dbContext.UserProjections
            .Where(p => splitUserIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);
        var nameById = userProjections.ToDictionary(
            p => p.UserId.Value,
            p => p.GetFullName());

        var windowEndExclusive = windowEnd.AddDays(1);

        // Project each split into (month, userId, item) tuples
        var projected = new List<(int Year, int Month, Guid UserId, bool IsClaimed, decimal Amount, string Currency, HouseholdContributionItem Item)>();

        foreach (var split in splits)
        {
            if (!billById.TryGetValue(split.BillId, out var bill)) continue;

            IEnumerable<DateTime> occurrenceDates;
            if (bill.RecurrenceSchedule != null)
            {
                occurrenceDates = bill.RecurrenceSchedule.GetOccurrencesInRange(windowStart, windowEndExclusive);
            }
            else
            {
                occurrenceDates = [bill.DueDate];
            }

            foreach (var date in occurrenceDates)
            {
                var isOriginalMonth = date.Year == bill.DueDate.Year && date.Month == bill.DueDate.Month;
                var isClaimed = isOriginalMonth && split.IsClaimed;

                projected.Add((date.Year, date.Month, split.UserId.Value, isClaimed,
                    split.Amount.Amount, split.Amount.Currency,
                    new HouseholdContributionItem(
                        split.Id.Value, bill.Id.Value,
                        bill.Title, bill.Category.ToString(),
                        split.Amount.Amount, split.Amount.Currency,
                        date, isClaimed)));
            }
        }

        // Build monthly buckets
        var monthCount = ((windowEnd.Year * 12 + windowEnd.Month) - (windowStart.Year * 12 + windowStart.Month)) + 1;
        var result = new List<HouseholdMonthlyContributions>(monthCount);

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
                    return new HouseholdMemberContribution(g.Key, displayName, totalDue, totalPaid, contributions);
                })
                .OrderBy(m2 => m2.UserId)
                .ToList();

            var total = byMember.Sum(m2 => m2.TotalDue);
            result.Add(new HouseholdMonthlyContributions(label, mStart, total, currency, byMember));
        }

        return result;
    }

    private static IReadOnlyCollection<HouseholdMonthlyContributions> BuildEmptyMonths(DateTime windowStart, DateTime windowEnd)
    {
        var monthCount = ((windowEnd.Year * 12 + windowEnd.Month) - (windowStart.Year * 12 + windowStart.Month)) + 1;
        return Enumerable.Range(0, monthCount)
            .Select(m =>
            {
                var mStart = windowStart.AddMonths(m);
                return new HouseholdMonthlyContributions(mStart.ToString("MMMM yyyy"), mStart, 0, "USD", []);
            })
            .ToList();
    }
}
