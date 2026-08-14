using Finance.Application.Queries;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class GroupQuery : IGroupQuery
{
    private readonly FinanceDbContext _db;

    public GroupQuery(FinanceDbContext db) => _db = db;

    public Task<bool> IsCurrentMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default) =>
        _db.GroupMemberProjections
            .AsNoTracking()
            .AnyAsync(p => p.GroupId == groupId && p.UserId == userId && p.IsActive, cancellationToken);

    public Task<bool> ExpenseBelongsToGroupAsync(Guid groupId, Guid expenseId, CancellationToken cancellationToken = default)
    {
        var id = ExpenseId.Create(expenseId);

        // Owner.Kind/Owner.Id, not the GroupId shorthand: that one is computed from Owner and has
        // no column, so EF cannot translate it and the whole query throws at runtime rather than
        // failing to compile. Every group route carrying an {expenseId} runs through here.
        return _db.Expenses.AsNoTracking().AnyAsync(
            e => e.Id == id && e.Owner.Kind == EntityKind.Group && e.Owner.Id == groupId,
            cancellationToken);
    }
}
