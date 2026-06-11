using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class ChargeRepository : IChargeRepository
{
    private readonly FinanceDbContext _dbContext;

    public ChargeRepository(FinanceDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(Charge expense, CancellationToken cancellationToken = default)
    {
        await _dbContext.Charges.AddAsync(expense, cancellationToken);
    }

    public async Task UpdateAsync(Charge expense, CancellationToken cancellationToken = default)
    {
        _dbContext.Charges.Update(expense);
    }

    public Task<Charge?> GetByIdAsync(ChargeId id, CancellationToken cancellationToken = default)
        => _dbContext.Charges.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task RemoveAsync(Charge expense, CancellationToken cancellationToken = default)
    {
        _dbContext.Charges.Remove(expense);
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public Task DeleteAllForUserAsync(UserId userId, CancellationToken cancellationToken = default)
        => _dbContext.Charges.Where(e => e.UserId == userId).ExecuteDeleteAsync(cancellationToken);
}
