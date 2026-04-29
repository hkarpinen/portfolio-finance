using Finance.Application.Contracts;
using Finance.Application.Queries;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class BillQuery : IBillQuery
{
    private readonly FinanceDbContext _db;

    public BillQuery(FinanceDbContext db) => _db = db;

    public async Task<BillListResponse> ListAsync(ListBillsRequest request, CancellationToken cancellationToken = default)
    {
        var query = _db.Bills.Where(b => b.HouseholdId == HouseholdId.Create(request.HouseholdId));
        if (request.ActiveOnly) query = query.Where(b => b.IsActive);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(b => b.DueDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new BillListResponse(items.Select(Map).ToArray(), total);
    }

    public async Task<BillResponse?> GetDetailAsync(BillDetailRequest request, CancellationToken cancellationToken = default)
    {
        var bill = await _db.Bills.FirstOrDefaultAsync(b => b.Id == BillId.Create(request.BillId), cancellationToken);
        return bill is null ? null : Map(bill);
    }

    public async Task<IReadOnlyCollection<SplitResponse>> ListSplitsAsync(ListSplitsRequest request, CancellationToken cancellationToken = default)
    {
        var splits = await _db.BillSplits
            .Where(s => s.BillId == BillId.Create(request.BillId))
            .ToListAsync(cancellationToken);

        return splits.Select(MapSplit).ToArray();
    }

    private static BillResponse Map(Bill bill) => new(
        bill.Id.Value,
        bill.HouseholdId.Value,
        bill.Title,
        bill.Description,
        bill.Amount.Amount,
        bill.Amount.Currency,
        bill.Category,
        bill.CreatedBy.Value,
        bill.DueDate,
        bill.RecurrenceSchedule?.Frequency,
        bill.RecurrenceSchedule?.StartDate,
        bill.RecurrenceSchedule?.EndDate,
        bill.IsActive,
        bill.CreatedAt,
        bill.UpdatedAt);

    private static SplitResponse MapSplit(BillSplit split) => new(
        split.Id.Value,
        split.BillId.Value,
        split.HouseholdId.Value,
        split.MembershipId.Value,
        split.UserId.Value,
        split.Amount.Amount,
        split.Amount.Currency,
        split.IsClaimed,
        split.ClaimedAt,
        split.ClaimedBy?.Value,
        split.CreatedAt,
        split.UpdatedAt);
}
