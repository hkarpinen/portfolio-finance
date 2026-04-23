using Bills.Application.Contracts;
using Bills.Application.Queries;
using Bills.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class BillSplitQuery : IBillSplitQuery
{
    private readonly BillsDbContext _dbContext;

    public BillSplitQuery(BillsDbContext dbContext) => _dbContext = dbContext;

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
}
