using Bills.Application.Contracts;
using Bills.Application.Queries;
using Bills.Domain.Aggregates;
using Bills.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class IncomeQuery : IIncomeQuery
{
    private readonly BillsDbContext _db;

    public IncomeQuery(BillsDbContext db) => _db = db;

    public async Task<IncomeListResponse> ListAsync(ListIncomeRequest request, CancellationToken cancellationToken = default)
    {
        var query = _db.IncomeSources.Where(i => i.HouseholdId == HouseholdId.Create(request.HouseholdId));
        if (request.ActiveOnly) query = query.Where(i => i.IsActive);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(i => i.Source)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new IncomeListResponse(items.Select(Map).ToArray(), total);
    }

    public async Task<IncomeListResponse> ListByUserAsync(ListUserIncomeRequest request, CancellationToken cancellationToken = default)
    {
        var query = _db.IncomeSources.Where(i => i.UserId == UserId.Create(request.UserId));
        if (request.ActiveOnly) query = query.Where(i => i.IsActive);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(i => i.Source)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new IncomeListResponse(items.Select(Map).ToArray(), total);
    }

    public async Task<IncomeResponse?> GetDetailAsync(IncomeDetailRequest request, CancellationToken cancellationToken = default)
    {
        var income = await _db.IncomeSources.FirstOrDefaultAsync(i => i.Id == IncomeId.Create(request.IncomeId), cancellationToken);
        return income is null ? null : Map(income);
    }

    private static IncomeResponse Map(IncomeSource income) => new(
        income.Id.Value,
        income.HouseholdId?.Value,
        income.MembershipId?.Value,
        income.UserId.Value,
        income.Amount.Amount,
        income.Amount.Currency,
        income.Source,
        income.RecurrenceSchedule.Frequency,
        income.RecurrenceSchedule.StartDate,
        income.RecurrenceSchedule.EndDate,
        income.IsActive,
        income.LastPaymentDate,
        income.CreatedAt,
        income.UpdatedAt);
}
