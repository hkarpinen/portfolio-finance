using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class HouseholdRepository : IHouseholdRepository
{
    private readonly FinanceDbContext _dbContext;

    public HouseholdRepository(FinanceDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(Household household, CancellationToken cancellationToken = default)
    {
        await _dbContext.Households.AddAsync(household, cancellationToken);
    }

    public async Task UpdateAsync(Household household, CancellationToken cancellationToken = default)
    {
        _dbContext.Households.Update(household);
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public Task<Household?> GetByIdAsync(HouseholdId householdId, CancellationToken cancellationToken = default)
        => _dbContext.Households.FirstOrDefaultAsync(h => h.Id == householdId, cancellationToken);
}
