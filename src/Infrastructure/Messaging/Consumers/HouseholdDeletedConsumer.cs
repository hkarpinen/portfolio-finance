using Finance.Domain.ValueObjects;
using Household.Domain.Events;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Messaging.Consumers;

// Tears down a group's finance state when its household is deleted (real deletion or demo expiry).
//
// Direct row deletion (not event-driven reversal) is acceptable HERE — and ONLY here — because the
// entire household graph is being destroyed: there is nothing left to reconcile the books against,
// so the usual "mutate financial state only via a domain event" rule does not apply. These events
// carry no event id, so dedup rides the transport MessageId; every delete is idempotent (a
// redelivery finds the rows already gone and no-ops).
internal sealed class HouseholdDeletedConsumer : IConsumer<HouseholdDeleted>
{
    private readonly FinanceDbContext _db;

    public HouseholdDeletedConsumer(FinanceDbContext db) => _db = db;

    public async Task Consume(ConsumeContext<HouseholdDeleted> context)
    {
        var ct = context.CancellationToken;
        var messageId = context.MessageId ?? Guid.NewGuid();
        if (await _db.ProcessedEvents.AnyAsync(e => e.EventId == messageId, ct))
            return;

        var groupId = context.Message.HouseholdId;

        // JournalLines cascade from journal_entries (fk_journalLines_journal_entries_entry_id, ON DELETE CASCADE),
        // so deleting entries removes their journal_lines; accounts and the ledger row go explicitly.
        var ledger = await _db.Ledgers.FirstOrDefaultAsync(
            l => l.Owner.Kind == EntityKind.Household && l.Owner.Id == groupId, ct);
        if (ledger is not null)
        {
            var ledgerId = ledger.Id;
            await _db.JournalEntries.Where(e => e.LedgerId == ledgerId).ExecuteDeleteAsync(ct);
            await _db.Accounts.Where(a => a.LedgerId == ledgerId).ExecuteDeleteAsync(ct);
            await _db.Ledgers.Where(l => l.Id == ledgerId).ExecuteDeleteAsync(ct);
        }

        await _db.GroupMemberProjections.Where(p => p.GroupId == groupId).ExecuteDeleteAsync(ct);

        var group = GroupId.Create(groupId);
        // An share's group is its expense's, so the shares go first, matched through the bills
        // they are on.
        await _db.Shares
            .Where(a => _db.Expenses.Any(c => c.Id == a.ExpenseId && c.GroupId == group))
            .ExecuteDeleteAsync(ct);
        await _db.Expenses.Where(c => c.GroupId == group).ExecuteDeleteAsync(ct);

        _db.ProcessedEvents.Add(new ProcessedEvent(messageId, nameof(HouseholdDeleted), DateTime.UtcNow));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { }
    }
}
