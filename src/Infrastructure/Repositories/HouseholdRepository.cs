using Bills.Application.Managers.Dependencies;
using Bills.Domain.Aggregates;
using Bills.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class HouseholdRepository : IHouseholdRepository
{
    private readonly BillsDbContext _dbContext;

    public HouseholdRepository(BillsDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(Household household, CancellationToken cancellationToken = default)
    {
        await _dbContext.Households.AddAsync(household, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Household household, CancellationToken cancellationToken = default)
    {
        _dbContext.Households.Update(household);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Household?> GetByIdAsync(HouseholdId householdId, CancellationToken cancellationToken = default)
        => _dbContext.Households.FirstOrDefaultAsync(h => h.Id == householdId, cancellationToken);
}
