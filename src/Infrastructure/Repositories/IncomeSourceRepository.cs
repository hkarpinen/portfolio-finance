using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class IncomeSourceRepository : IIncomeSourceRepository
{
    private readonly FinanceDbContext _dbContext;

    public IncomeSourceRepository(FinanceDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(IncomeSource incomeSource, CancellationToken cancellationToken = default)
    {
        await _dbContext.IncomeSources.AddAsync(incomeSource, cancellationToken);
    }

    public async Task UpdateAsync(IncomeSource incomeSource, CancellationToken cancellationToken = default)
    {
        _dbContext.IncomeSources.Update(incomeSource);
    }

    public Task<IncomeSource?> GetByIdAsync(IncomeId incomeId, CancellationToken cancellationToken = default)
        => _dbContext.IncomeSources.FirstOrDefaultAsync(i => i.Id == incomeId, cancellationToken);

    public async Task RemoveAsync(IncomeSource incomeSource, CancellationToken cancellationToken = default)
    {
        _dbContext.IncomeSources.Remove(incomeSource);
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
