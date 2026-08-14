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
        var gid = GroupId.Create(groupId);
        return _db.Expenses.AsNoTracking().AnyAsync(c => c.Id == id && c.GroupId == gid, cancellationToken);
    }
}
