using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class ShareRepository : IShareRepository
{
    private readonly FinanceDbContext _dbContext;

    public ShareRepository(FinanceDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(Share share, CancellationToken cancellationToken = default)
    {
        await _dbContext.Shares.AddAsync(share, cancellationToken);
    }

    public async Task UpdateAsync(Share share, CancellationToken cancellationToken = default)
    {
        _dbContext.Shares.Update(share);
    }

    public async Task RemoveAsync(Share share, CancellationToken cancellationToken = default)
    {
        _dbContext.Shares.Remove(share);
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public Task<Share?> GetByIdAsync(ShareId shareId, CancellationToken cancellationToken = default)
        => _dbContext.Shares.FirstOrDefaultAsync(s => s.Id == shareId, cancellationToken);

    public Task<Share?> GetByExpenseAndUserAsync(ExpenseId expenseId, UserId userId, CancellationToken cancellationToken = default)
        => _dbContext.Shares.FirstOrDefaultAsync(s => s.ExpenseId == expenseId && s.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<Share>> ListByExpenseAsync(ExpenseId expenseId, CancellationToken cancellationToken = default)
        => await _dbContext.Shares.Where(s => s.ExpenseId == expenseId).ToListAsync(cancellationToken);

    public async Task<decimal> SumForExpenseAsync(ExpenseId expenseId, ShareId? excluding = null, CancellationToken cancellationToken = default)
        => await _dbContext.Shares
            .AsNoTracking()
            .Where(a => a.ExpenseId == expenseId && (excluding == null || a.Id != excluding))
            .SumAsync(a => a.Amount.Amount, cancellationToken);

    public Task DeleteAllForUserAsync(UserId userId, CancellationToken cancellationToken = default)
        => _dbContext.Shares.Where(s => s.UserId == userId).ExecuteDeleteAsync(cancellationToken);
}
