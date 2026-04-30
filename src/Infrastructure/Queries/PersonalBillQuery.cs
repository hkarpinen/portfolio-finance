using Finance.Application.Contracts;
using Finance.Application.Mappers;
using Finance.Application.Queries;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class PersonalBillQuery : IPersonalBillQuery
{
    private readonly FinanceDbContext _db;

    public PersonalBillQuery(FinanceDbContext db) => _db = db;

    public async Task<PersonalBillListResponse> ListByUserAsync(ListPersonalBillsRequest request, CancellationToken cancellationToken = default)
    {
        var query = _db.PersonalBills.Where(b => b.UserId == UserId.Create(request.UserId));
        if (request.ActiveOnly) query = query.Where(b => b.IsActive);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(b => b.DueDate)
            .ThenBy(b => b.Title)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PersonalBillListResponse(items.Select(PersonalBillMapper.ToResponse).ToArray(), total);
    }

    public async Task<PersonalBillResponse?> GetDetailAsync(PersonalBillDetailRequest request, CancellationToken cancellationToken = default)
    {
        var bill = await _db.PersonalBills.FirstOrDefaultAsync(b => b.Id == PersonalBillId.Create(request.PersonalBillId), cancellationToken);
        return bill is null ? null : PersonalBillMapper.ToResponse(bill);
    }
}
