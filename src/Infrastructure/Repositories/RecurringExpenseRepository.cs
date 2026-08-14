using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class RecurringExpenseRepository(FinanceDbContext db) : IRecurringExpenseRepository
{
    public async Task AddAsync(RecurringExpense schedule, CancellationToken ct = default)
        => await db.RecurringExpenses.AddAsync(schedule, ct);

    public Task<RecurringExpense?> GetByIdAsync(RecurringExpenseId id, CancellationToken ct = default)
        => db.RecurringExpenses.FirstOrDefaultAsync(s => s.Id == id, ct);

    // Owner.Kind/Owner.Id, never the computed GroupId: it has no column, so EF cannot translate
    // it and the query throws when it runs — see GroupQuery.ExpenseBelongsToGroupAsync.
    public async Task<IReadOnlyList<RecurringExpense>> ListForGroupAsync(GroupId groupId, CancellationToken ct = default)
        => await db.RecurringExpenses.AsNoTracking()
            .Where(s => s.Owner.Kind == EntityKind.Group && s.Owner.Id == groupId.Value && s.IsActive)
            .OrderBy(s => s.Title)
            .ToListAsync(ct);

    /// <summary>Somebody's own agreements — the ones owned by the person, not by a group.</summary>
    public async Task<IReadOnlyList<RecurringExpense>> ListForUserAsync(UserId userId, CancellationToken ct = default)
        => await db.RecurringExpenses.AsNoTracking()
            .Where(s => s.Owner.Kind == EntityKind.Person && s.Owner.Id == userId.Value && s.IsActive)
            .OrderBy(s => s.Title)
            .ToListAsync(ct);

    public Task<Expense?> GetGeneratedAsync(RecurringExpenseId recurringExpenseId, DateTime occurrenceDate, CancellationToken ct = default)
    {
        var day = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc);
        return db.Expenses.FirstOrDefaultAsync(
            c => c.RecurringExpenseId == recurringExpenseId && c.OccurrenceDate == day, ct);
    }

    public async Task<IReadOnlyDictionary<DateTime, Expense>> ListGeneratedAsync(
        RecurringExpenseId recurringExpenseId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var first = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var last = DateTime.SpecifyKind(to.Date, DateTimeKind.Utc);

        var rows = await db.Expenses.AsNoTracking()
            .Where(c => c.RecurringExpenseId == recurringExpenseId && c.OccurrenceDate >= first && c.OccurrenceDate <= last)
            .ToListAsync(ct);

        return rows.ToDictionary(c => c.OccurrenceDate);
    }

    public Task CommitAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
