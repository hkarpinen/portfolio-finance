using Finance.Application.Contracts;
using Finance.Application.Managers.Dependencies;
using Finance.Application.Mappers;
using Finance.Application.Queries;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class IncomeQuery : IIncomeQuery
{
    private readonly FinanceDbContext _db;
    private readonly IIncomeSourceRepository _incomeRepository;
    private readonly IPayrollDeductionEngine _deductionEngine;

    public IncomeQuery(FinanceDbContext db, IIncomeSourceRepository incomeRepository, IPayrollDeductionEngine deductionEngine)
    {
        _db = db;
        _incomeRepository = incomeRepository;
        _deductionEngine = deductionEngine;
    }

    public async Task<IncomeListResponse> ListAsync(ListIncomeRequest request, CancellationToken cancellationToken = default)
    {
        var memberUserIds = await _db.HouseholdMemberships
            .Where(m => m.HouseholdId == HouseholdId.Create(request.HouseholdId) && m.IsActive)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        var query = _db.IncomeSources.Where(i => memberUserIds.Contains(i.UserId));
        if (request.ActiveOnly) query = query.Where(i => i.IsActive);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(i => i.Source)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new IncomeListResponse(items.Select(IncomeMapper.ToResponse).ToArray(), total);
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

        return new IncomeListResponse(items.Select(IncomeMapper.ToResponse).ToArray(), total);
    }

    public async Task<IncomeResponse?> GetDetailAsync(IncomeDetailRequest request, CancellationToken cancellationToken = default)
    {
        var income = await _db.IncomeSources.FirstOrDefaultAsync(i => i.Id == IncomeId.Create(request.IncomeId), cancellationToken);
        return income is null ? null : IncomeMapper.ToResponse(income);
    }

    public async Task<NetPayBreakdownResponse?> GetNetPayBreakdownAsync(GetNetPayBreakdownRequest request, CancellationToken cancellationToken = default)
    {
        var income = await _incomeRepository.GetByIdAsync(IncomeId.Create(request.IncomeId), cancellationToken);
        if (income is null) return null;

        return _deductionEngine.ComputeBreakdown(IncomeMapper.ToResponse(income), request.Year, request.Month);
    }
}
