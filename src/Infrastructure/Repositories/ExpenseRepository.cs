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

    public Task<Expense?> GetByIdAsync(ExpenseId id, CancellationToken cancellationToken = default)
        => _dbContext.Expenses.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public Task<bool> ExistsForUserAsync(UserId userId, string title, decimal amount, CancellationToken cancellationToken = default)
        => _dbContext.Expenses.AnyAsync(
            e => e.UserId == userId && e.IsActive && e.Title == title && e.Amount.Amount == amount,
            cancellationToken);

    public async Task RemoveAsync(Expense expense, CancellationToken cancellationToken = default)
    {
        _dbContext.Expenses.Remove(expense);
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
