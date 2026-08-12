using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class AllocationRepository : IAllocationRepository
{
    private readonly FinanceDbContext _dbContext;

    public AllocationRepository(FinanceDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(Allocation split, CancellationToken cancellationToken = default)
    {
        await _dbContext.Allocations.AddAsync(split, cancellationToken);
    }

    public async Task UpdateAsync(Allocation split, CancellationToken cancellationToken = default)
    {
        _dbContext.Allocations.Update(split);
    }

    public async Task RemoveAsync(Allocation split, CancellationToken cancellationToken = default)
    {
        _dbContext.Allocations.Remove(split);
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public Task<Allocation?> GetByIdAsync(AllocationId splitId, CancellationToken cancellationToken = default)
        => _dbContext.Allocations.FirstOrDefaultAsync(s => s.Id == splitId, cancellationToken);

    public Task<Allocation?> GetByChargeAndUserAsync(ChargeId expenseId, UserId userId, CancellationToken cancellationToken = default)
        => _dbContext.Allocations.FirstOrDefaultAsync(s => s.ChargeId == expenseId && s.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<Allocation>> ListByChargeAsync(ChargeId chargeId, CancellationToken cancellationToken = default)
        => await _dbContext.Allocations.Where(s => s.ChargeId == chargeId).ToListAsync(cancellationToken);

    public async Task<decimal> SumForChargeAsync(ChargeId chargeId, AllocationId? excluding = null, CancellationToken cancellationToken = default)
        => await _dbContext.Allocations
            .AsNoTracking()
            .Where(a => a.ChargeId == chargeId && (excluding == null || a.Id != excluding))
            .SumAsync(a => a.Amount.Amount, cancellationToken);

    public Task DeleteAllForUserAsync(UserId userId, CancellationToken cancellationToken = default)
        => _dbContext.Allocations.Where(s => s.UserId == userId).ExecuteDeleteAsync(cancellationToken);
}
