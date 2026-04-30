using Finance.Application.Contracts;
using Finance.Application.Mappers;
using Finance.Application.Queries;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class HouseholdQuery : IHouseholdQuery
{
    private readonly FinanceDbContext _db;

    public HouseholdQuery(FinanceDbContext db) => _db = db;

    public async Task<HouseholdListResponse> ListAsync(ListHouseholdsRequest request, CancellationToken cancellationToken = default)
    {
        var query = _db.Households.AsQueryable();
        if (request.ActiveOnly) query = query.Where(h => h.IsActive);
        if (request.UserId.HasValue) query = query.Where(h => h.OwnerId == UserId.Create(request.UserId.Value));

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(h => h.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new HouseholdListResponse(items.Select(HouseholdMapper.ToResponse).ToArray(), total);
    }

    public async Task<HouseholdResponse?> GetDetailAsync(HouseholdDetailRequest request, CancellationToken cancellationToken = default)
    {
        var household = await _db.Households.FirstOrDefaultAsync(h => h.Id == HouseholdId.Create(request.HouseholdId), cancellationToken);
        return household is null ? null : HouseholdMapper.ToResponse(household);
    }
}
