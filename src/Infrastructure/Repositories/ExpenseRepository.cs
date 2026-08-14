using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class ExpenseRepository : IExpenseRepository
{
    private readonly FinanceDbContext _dbContext;

    public ExpenseRepository(FinanceDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        await _dbContext.Expenses.AddAsync(expense, cancellationToken);
    }

    public async Task UpdateAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        _dbContext.Expenses.Update(expense);
    }

    public async Task<IReadOnlyList<Expense>> ListUnpostedPersonalAsync(UserId userId, DateTime asOf, CancellationToken cancellationToken = default)
    {
        var day = DateTime.SpecifyKind(asOf.Date, DateTimeKind.Utc);
        return await _dbContext.Expenses
            .Where(c => c.Owner.Kind == EntityKind.Person && c.Owner.Id == userId.Value
                        && c.IsActive && c.OccurrenceDate <= day)
            .ToListAsync(cancellationToken);
    }

    public Task<Expense?> GetByIdAsync(ExpenseId id, CancellationToken cancellationToken = default)
        => _dbContext.Expenses.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task RemoveAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        _dbContext.Expenses.Remove(expense);
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public Task DeleteAllForUserAsync(UserId userId, CancellationToken cancellationToken = default)
        => _dbContext.Expenses.Where(e => e.Owner.Kind == EntityKind.Person && e.Owner.Id == userId.Value)
            .ExecuteDeleteAsync(cancellationToken);
}
