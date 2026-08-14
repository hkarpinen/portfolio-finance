using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

public interface IExpenseRepository
{
    Task AddAsync(Expense expense, CancellationToken cancellationToken = default);
    Task UpdateAsync(Expense expense, CancellationToken cancellationToken = default);
    Task RemoveAsync(Expense expense, CancellationToken cancellationToken = default);
    Task<Expense?> GetByIdAsync(ExpenseId id, CancellationToken cancellationToken = default);
    /// <summary>
    /// This person's own expenses that have come due — the set a catch-up posts. Cheap to over-
    /// fetch: converging one already on the books is a no-op, so the filter is on date, not on
    /// whether an entry exists.
    /// </summary>
    Task<IReadOnlyList<Expense>> ListUnpostedPersonalAsync(UserId userId, DateTime asOf, CancellationToken cancellationToken = default);

    Task CommitAsync(CancellationToken cancellationToken = default);
    Task DeleteAllForUserAsync(UserId userId, CancellationToken cancellationToken = default);
}
