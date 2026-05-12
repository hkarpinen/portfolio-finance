using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class ExpenseSplitRepository : IExpenseSplitRepository
{
    private readonly FinanceDbContext _dbContext;

    public ExpenseSplitRepository(FinanceDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(ExpenseSplit split, CancellationToken cancellationToken = default)
    {
        await _dbContext.ExpenseSplits.AddAsync(split, cancellationToken);
    }

    public async Task UpdateAsync(ExpenseSplit split, CancellationToken cancellationToken = default)
    {
        _dbContext.ExpenseSplits.Update(split);
    }

    public async Task RemoveAsync(ExpenseSplit split, CancellationToken cancellationToken = default)
    {
        _dbContext.ExpenseSplits.Remove(split);
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public Task<ExpenseSplit?> GetByIdAsync(ExpenseSplitId splitId, CancellationToken cancellationToken = default)
        => _dbContext.ExpenseSplits.FirstOrDefaultAsync(s => s.Id == splitId, cancellationToken);

    public Task<ExpenseSplit?> GetByExpenseAndMembershipAsync(ExpenseId expenseId, MembershipId membershipId, CancellationToken cancellationToken = default)
        => _dbContext.ExpenseSplits.FirstOrDefaultAsync(s => s.ExpenseId == expenseId && s.MembershipId == membershipId, cancellationToken);

    public Task<ExpenseSplit?> GetByExpenseAndUserAsync(ExpenseId expenseId, UserId userId, CancellationToken cancellationToken = default)
        => _dbContext.ExpenseSplits.FirstOrDefaultAsync(s => s.ExpenseId == expenseId && s.UserId == userId, cancellationToken);
}
