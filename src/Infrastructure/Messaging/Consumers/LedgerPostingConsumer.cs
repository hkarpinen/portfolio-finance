using Finance.Application.Managers;
using Finance.Domain.Events;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Messaging.Consumers;

/// <summary>
/// Keeps the double-entry ledger in step with the charge context by consuming finance's OWN
/// domain events off the bus (outbox → RabbitMQ → here). The aggregate write and its outbox row
/// commit in one transaction, so once a charge/allocation/settlement fact is saved its ledger
/// posting is guaranteed to follow — a failed posting is redelivered rather than lost, which is
/// what the old "controller coordinates two managers with two commits" pattern could not promise.
///
/// This consumer is a thin adapter: it dedups on the event id and DISPATCHES to the
/// <see cref="IBookkeepingManager"/>, which owns the domain reads and convergence orchestration
/// (re-reading the aggregate and syncing the books to it). The consumer itself does no charge /
/// allocation I/O — only its own processed-events bookkeeping. Convergence makes processing
/// idempotent (redeliveries no-op) and order-insensitive.
/// </summary>
internal sealed class LedgerPostingConsumer :
    IConsumer<ChargeCreated>,
    IConsumer<ChargeUpdated>,
    IConsumer<ChargeActivated>,
    IConsumer<ChargeDeactivated>,
    IConsumer<AllocationCreated>,
    IConsumer<AllocationUpdated>,
    IConsumer<AllocationRemoved>,
    IConsumer<SettlementRecorded>,
    IConsumer<SettlementReversed>,
    IConsumer<VendorPaid>,
    IConsumer<VendorPaymentReversed>
{
    private readonly FinanceDbContext _db;
    private readonly IBookkeepingManager _bookkeeping;

    public LedgerPostingConsumer(FinanceDbContext db, IBookkeepingManager bookkeeping)
    {
        _db = db;
        _bookkeeping = bookkeeping;
    }

    // ── Charge lifecycle ─────────────────────────────────────────────────────

    public Task Consume(ConsumeContext<ChargeCreated> context) =>
        HandleAsync(context.Message, nameof(ChargeCreated),
            ct => _bookkeeping.ConvergeChargeAsync(context.Message.ChargeId.Value, ct), context.CancellationToken);

    public Task Consume(ConsumeContext<ChargeUpdated> context) =>
        HandleAsync(context.Message, nameof(ChargeUpdated),
            ct => _bookkeeping.ConvergeChargeAsync(context.Message.ChargeId.Value, ct), context.CancellationToken);

    public Task Consume(ConsumeContext<ChargeActivated> context) =>
        HandleAsync(context.Message, nameof(ChargeActivated),
            ct => _bookkeeping.ConvergeChargeAsync(context.Message.ChargeId.Value, ct), context.CancellationToken);

    public Task Consume(ConsumeContext<ChargeDeactivated> context) =>
        HandleAsync(context.Message, nameof(ChargeDeactivated), ct =>
        {
            var m = context.Message;
            if (m.GroupId is null) return Task.CompletedTask; // personal charges never touch the group ledger
            // Unwind the charge from the books — accrual, vendor payment and any settlements — so a
            // deleted bill leaves no orphan Vendor Payable / member balances.
            return _bookkeeping.ReverseChargeAsync(m.GroupId.Value.Value, m.ChargeId.Value, ct);
        }, context.CancellationToken);

    // ── Allocations ──────────────────────────────────────────────────────────

    public Task Consume(ConsumeContext<AllocationCreated> context) =>
        HandleAsync(context.Message, nameof(AllocationCreated),
            ct => _bookkeeping.ConvergeAllocationAsync(context.Message.GroupId.Value, context.Message.AllocationId.Value, ct),
            context.CancellationToken);

    public Task Consume(ConsumeContext<AllocationUpdated> context) =>
        HandleAsync(context.Message, nameof(AllocationUpdated),
            ct => _bookkeeping.ConvergeAllocationAsync(context.Message.GroupId.Value, context.Message.AllocationId.Value, ct),
            context.CancellationToken);

    public Task Consume(ConsumeContext<AllocationRemoved> context) =>
        HandleAsync(context.Message, nameof(AllocationRemoved),
            ct => _bookkeeping.ReverseBySourceAsync(
                context.Message.GroupId.Value,
                LedgerSources.Allocation(context.Message.AllocationId.Value), ct),
            context.CancellationToken);

    // ── Settlements (member settles their share) ────────────────────────────

    public Task Consume(ConsumeContext<SettlementRecorded> context) =>
        HandleAsync(context.Message, nameof(SettlementRecorded), ct =>
        {
            var m = context.Message;
            return _bookkeeping.RecordSettlementFromEventAsync(
                m.GroupId.Value, m.ChargeId.Value, m.AllocationId.Value,
                m.FromUserId.Value, m.ToUserId.Value, m.Amount.Amount, m.Amount.Currency,
                m.OccurrenceDate, m.ValueDate, ct);
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<SettlementReversed> context) =>
        HandleAsync(context.Message, nameof(SettlementReversed), ct =>
        {
            var m = context.Message;
            return _bookkeeping.ReverseBySourceAsync(
                m.GroupId.Value,
                LedgerSources.Settlement(m.ChargeId.Value, m.OccurrenceDate, m.FromUserId.Value), ct);
        }, context.CancellationToken);

    // ── Vendor payment (the bill itself) ────────────────────────────────────

    public Task Consume(ConsumeContext<VendorPaid> context) =>
        HandleAsync(context.Message, nameof(VendorPaid), ct =>
        {
            var m = context.Message;
            return _bookkeeping.RecordVendorPaymentFromEventAsync(
                m.ChargeId.Value, m.FundingSource, m.PaidByUserId, m.OccurrenceDate, m.PaidAt.Date, ct);
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<VendorPaymentReversed> context) =>
        HandleAsync(context.Message, nameof(VendorPaymentReversed), ct =>
        {
            var m = context.Message;
            return _bookkeeping.ReverseVendorPaymentFromEventAsync(m.ChargeId.Value, m.OccurrenceDate, ct);
        }, context.CancellationToken);

    // ── Dedup wrapper ─────────────────────────────────────────────────────────

    /// <summary>Standard processed-events dedup around a handler: skip if this event id was already
    /// processed, run the handler, then record the id. Bookkeeping commits before the marker is
    /// saved — a crash in between redelivers the event, and the convergent handlers no-op.</summary>
    private async Task HandleAsync(DomainEvent message, string eventType, Func<CancellationToken, Task> handler, CancellationToken ct)
    {
        if (await _db.ProcessedEvents.AnyAsync(e => e.EventId == message.EventId, ct))
            return;

        try
        {
            await handler(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // A concurrent writer already posted this entry under the same (ledger, source) — the
            // partial unique index on journal_entries rejected our duplicate. The books already
            // hold the posting, so this delivery is a no-op. We return without recording the marker
            // (the rejected insert is still tracked on this scoped context); the message is ACKed.
            return;
        }

        _db.ProcessedEvents.Add(new ProcessedEvent(message.EventId, eventType, DateTime.UtcNow));
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex)) { }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

/// <summary>
/// One queue, one message at a time: every ledger posting serializes through this endpoint, so
/// reverse-then-repost sequences from different events can never interleave into duplicate or
/// missing postings. Throughput is bounded by ledger volume, which is human-scale here.
/// </summary>
internal sealed class LedgerPostingConsumerDefinition : ConsumerDefinition<LedgerPostingConsumer>
{
    public LedgerPostingConsumerDefinition()
    {
        ConcurrentMessageLimit = 1;
    }
}
