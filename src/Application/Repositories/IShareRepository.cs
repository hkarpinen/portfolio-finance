using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

public interface IShareRepository
{
    Task AddAsync(Share split, CancellationToken cancellationToken = default);
    Task UpdateAsync(Share split, CancellationToken cancellationToken = default);
    Task RemoveAsync(Share split, CancellationToken cancellationToken = default);
    Task<Share?> GetByIdAsync(ShareId splitId, CancellationToken cancellationToken = default);
    Task<Share?> GetByExpenseAndUserAsync(ExpenseId expenseId, UserId userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Share>> ListByExpenseAsync(ExpenseId expenseId, CancellationToken cancellationToken = default);

    /// <summary>What a expense is already split by, optionally ignoring one share — the shape
    /// an upsert needs, so it can ask "does mine still fit alongside the others".</summary>
    Task<decimal> SumForExpenseAsync(ExpenseId expenseId, ShareId? excluding = null, CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task DeleteAllForUserAsync(UserId userId, CancellationToken cancellationToken = default);
}
