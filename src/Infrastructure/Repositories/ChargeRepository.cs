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

    public async Task<IReadOnlyList<Charge>> ListUnpostedPersonalAsync(UserId userId, DateTime asOf, CancellationToken cancellationToken = default)
    {
        var day = DateTime.SpecifyKind(asOf.Date, DateTimeKind.Utc);
        return await _dbContext.Charges
            .Where(c => c.Owner.Kind == EntityKind.Person && c.Owner.Id == userId.Value
                        && c.IsActive && c.OccurrenceDate <= day)
            .ToListAsync(cancellationToken);
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
        => _dbContext.Charges.Where(e => e.Owner.Kind == EntityKind.Person && e.Owner.Id == userId.Value)
            .ExecuteDeleteAsync(cancellationToken);
}
