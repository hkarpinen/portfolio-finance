using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

public interface IExpenseSplitRepository
{
    Task AddAsync(ExpenseSplit split, CancellationToken cancellationToken = default);
    Task UpdateAsync(ExpenseSplit split, CancellationToken cancellationToken = default);
    Task RemoveAsync(ExpenseSplit split, CancellationToken cancellationToken = default);
    Task<ExpenseSplit?> GetByIdAsync(ExpenseSplitId splitId, CancellationToken cancellationToken = default);
    Task<ExpenseSplit?> GetByExpenseAndMembershipAsync(ExpenseId expenseId, MembershipId membershipId, CancellationToken cancellationToken = default);
    Task<ExpenseSplit?> GetByExpenseAndUserAsync(ExpenseId expenseId, UserId userId, CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
}
