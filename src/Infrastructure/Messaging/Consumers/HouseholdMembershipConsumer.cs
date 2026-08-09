using Finance.Infrastructure.Persistence.Projections;
using Household.Domain.Events;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Messaging.Consumers;

// The projection is read-side only: a departing member's ledger accounts and balances stay on the
// books (debt does not vanish with departure); this just lets queries tell current members from
// departed ones and label allocations with real roles. These events carry no event id, so dedup
// rides the transport MessageId — and every handler is a state upsert, so redelivery is harmless
// either way.
internal sealed class HouseholdMembershipConsumer :
    IConsumer<HouseholdMemberJoined>,
    IConsumer<HouseholdMemberLeft>,
    IConsumer<HouseholdMemberRemoved>,
    IConsumer<HouseholdMemberRoleChanged>
{
    private readonly FinanceDbContext _db;

    public HouseholdMembershipConsumer(FinanceDbContext db) => _db = db;

    public Task Consume(ConsumeContext<HouseholdMemberJoined> context) =>
        HandleAsync(context, nameof(HouseholdMemberJoined), async ct =>
        {
            var m = context.Message;
            var existing = await FindAsync(m.HouseholdId, m.UserId, ct);
            if (existing is null)
                _db.GroupMemberProjections.Add(GroupMemberProjection.Create(m.HouseholdId, m.UserId, m.Role, m.JoinedAt));
            else
                existing.Rejoin(m.Role, m.JoinedAt);
        });

    public Task Consume(ConsumeContext<HouseholdMemberLeft> context) =>
        HandleAsync(context, nameof(HouseholdMemberLeft), async ct =>
        {
            var m = context.Message;
            (await FindAsync(m.HouseholdId, m.UserId, ct))?.Depart(m.LeftAt);
        });

    public Task Consume(ConsumeContext<HouseholdMemberRemoved> context) =>
        HandleAsync(context, nameof(HouseholdMemberRemoved), async ct =>
        {
            var m = context.Message;
            (await FindAsync(m.HouseholdId, m.RemovedUserId, ct))?.Depart(m.RemovedAt);
        });

    public Task Consume(ConsumeContext<HouseholdMemberRoleChanged> context) =>
        HandleAsync(context, nameof(HouseholdMemberRoleChanged), async ct =>
        {
            var m = context.Message;
            (await FindAsync(m.HouseholdId, m.UserId, ct))?.ChangeRole(m.NewRole);
        });

    private Task<GroupMemberProjection?> FindAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        _db.GroupMemberProjections.FirstOrDefaultAsync(p => p.GroupId == groupId && p.UserId == userId, ct);

    private async Task HandleAsync<T>(ConsumeContext<T> context, string eventType, Func<CancellationToken, Task> handler)
        where T : class
    {
        var messageId = context.MessageId ?? Guid.NewGuid();
        if (await _db.ProcessedEvents.AnyAsync(e => e.EventId == messageId, context.CancellationToken))
            return;

        await handler(context.CancellationToken);

        _db.ProcessedEvents.Add(new ProcessedEvent(messageId, eventType, DateTime.UtcNow));
        try
        {
            await _db.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { }
    }
}
