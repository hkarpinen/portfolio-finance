using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

/// <summary>Persistence only — amending and deactivating happen on the aggregate.</summary>
public interface IRecurringExpenseRepository
{
    Task AddAsync(RecurringExpense schedule, CancellationToken ct = default);
    Task<RecurringExpense?> GetByIdAsync(RecurringExpenseId id, CancellationToken ct = default);
    Task<IReadOnlyList<RecurringExpense>> ListForGroupAsync(GroupId groupId, CancellationToken ct = default);
    Task<IReadOnlyList<RecurringExpense>> ListForUserAsync(UserId userId, CancellationToken ct = default);

    /// <summary>The expense already generated for that occurrence, or null. Keyed the same way the
    /// unique index is, so "has this month been recorded" is one lookup.</summary>
    Task<Expense?> GetGeneratedAsync(RecurringExpenseId recurringExpenseId, DateTime occurrenceDate, CancellationToken ct = default);

    /// <summary>Everything generated for a window, keyed by occurrence — one query, not one a date.</summary>
    Task<IReadOnlyDictionary<DateTime, Expense>> ListGeneratedAsync(
        RecurringExpenseId recurringExpenseId, DateTime from, DateTime to, CancellationToken ct = default);

    Task CommitAsync(CancellationToken ct = default);
}
