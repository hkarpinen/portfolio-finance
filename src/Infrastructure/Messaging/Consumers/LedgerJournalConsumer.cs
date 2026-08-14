using Finance.Application.Managers;
using Finance.Domain.Events;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Messaging.Consumers;

// Keeps the double-entry ledger in step with the expense context by consuming finance's OWN domain
// events off the bus. The aggregate write and its outbox row commit in one transaction, so once a
// expense/share/settlement fact is saved its ledger journalLine is guaranteed to follow — a failed
// journalLine is redelivered rather than lost.
//
// A thin adapter: it dedups on the event id and dispatches. It does no expense or share I/O of
// its own, only its processed-events bookkeeping. The handlers converge (re-read the aggregate,
// sync the books to it), which makes processing idempotent and order-insensitive.
internal sealed class LedgerJournalLineConsumer :
    IConsumer<ExpenseCreated>,
    IConsumer<ExpenseUpdated>,
    IConsumer<ExpenseActivated>,
    IConsumer<ExpensePaid>,
    IConsumer<ExpenseUnpaid>,
    IConsumer<ExpenseDeactivated>,
    IConsumer<ShareCreated>,
    IConsumer<ShareUpdated>,
    IConsumer<ShareRemoved>,
    IConsumer<SettlementRecorded>,
    IConsumer<MemberTransferRecorded>,
    IConsumer<ReceiptRecorded>,
    IConsumer<ReceiptVoided>,
    IConsumer<SettlementReversed>,
    IConsumer<VendorPaid>,
    IConsumer<VendorPaymentReversed>
{
    private readonly FinanceDbContext _db;
    private readonly IBookkeepingManager _bookkeeping;

    public LedgerJournalLineConsumer(FinanceDbContext db, IBookkeepingManager bookkeeping)
    {
        _db = db;
        _bookkeeping = bookkeeping;
    }

    public Task Consume(ConsumeContext<ExpenseCreated> context) =>
        HandleAsync(context.Message, nameof(ExpenseCreated),
            ct => _bookkeeping.ConvergeExpenseAsync(context.Message.ExpenseId.Value, ct), context.CancellationToken);

    public Task Consume(ConsumeContext<ExpenseUpdated> context) =>
        HandleAsync(context.Message, nameof(ExpenseUpdated),
            ct => _bookkeeping.ConvergeExpenseAsync(context.Message.ExpenseId.Value, ct), context.CancellationToken);

    public Task Consume(ConsumeContext<ExpenseActivated> context) =>
        HandleAsync(context.Message, nameof(ExpenseActivated),
            ct => _bookkeeping.ConvergeExpenseAsync(context.Message.ExpenseId.Value, ct), context.CancellationToken);

    public Task Consume(ConsumeContext<ExpensePaid> context) =>
        HandleAsync(context.Message, nameof(ExpensePaid),
            ct => _bookkeeping.RecordPersonalPaymentAsync(
                context.Message.ExpenseId.Value, null, context.Message.PaidAt, ct),
            context.CancellationToken);

    public Task Consume(ConsumeContext<ExpenseUnpaid> context) =>
        HandleAsync(context.Message, nameof(ExpenseUnpaid),
            ct => _bookkeeping.ReversePersonalPaymentAsync(context.Message.ExpenseId.Value, ct),
            context.CancellationToken);

    public Task Consume(ConsumeContext<ExpenseDeactivated> context) =>
        HandleAsync(context.Message, nameof(ExpenseDeactivated), ct =>
        {
            var m = context.Message;
            // A personal expense unwinds inside the person's own book; ConvergePersonalExpense
            // reverses it once the expense reads inactive.
            if (m.GroupId is null) return _bookkeeping.ConvergePersonalExpenseAsync(m.ExpenseId.Value, ct);
            return _bookkeeping.ReverseExpenseAsync(m.GroupId.Value.Value, m.ExpenseId.Value, ct);
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<ShareCreated> context) =>
        HandleAsync(context.Message, nameof(ShareCreated),
            ct => _bookkeeping.ConvergeShareAsync(context.Message.GroupId.Value, context.Message.ShareId.Value, ct),
            context.CancellationToken);

    public Task Consume(ConsumeContext<ShareUpdated> context) =>
        HandleAsync(context.Message, nameof(ShareUpdated),
            ct => _bookkeeping.ConvergeShareAsync(context.Message.GroupId.Value, context.Message.ShareId.Value, ct),
            context.CancellationToken);

    public Task Consume(ConsumeContext<ShareRemoved> context) =>
        HandleAsync(context.Message, nameof(ShareRemoved),
            ct => _bookkeeping.ReverseBySourceAsync(
                context.Message.GroupId.Value,
                LedgerSources.Share(context.Message.ShareId.Value), ct),
            context.CancellationToken);

    public Task Consume(ConsumeContext<ReceiptRecorded> context) =>
        HandleAsync(context.Message, nameof(ReceiptRecorded),
            ct => _bookkeeping.ConvergeReceiptAsync(context.Message.ReceiptId.Value, ct),
            context.CancellationToken);

    public Task Consume(ConsumeContext<ReceiptVoided> context) =>
        HandleAsync(context.Message, nameof(ReceiptVoided),
            ct => _bookkeeping.ReverseReceiptAsync(context.Message.ReceiptId.Value, ct),
            context.CancellationToken);

    public Task Consume(ConsumeContext<MemberTransferRecorded> context) =>
        HandleAsync(context.Message, nameof(MemberTransferRecorded), ct =>
        {
            var m = context.Message;
            return _bookkeeping.RecordMemberTransferAsync(
                m.GroupId.Value, m.FromUserId.Value, m.ToUserId.Value,
                m.Amount.Amount, m.Amount.Currency, $"settleup:{m.TransferId.Value:N}", ct);
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<SettlementRecorded> context) =>
        HandleAsync(context.Message, nameof(SettlementRecorded), ct =>
        {
            var m = context.Message;
            return _bookkeeping.RecordSettlementFromEventAsync(
                m.GroupId.Value, m.ExpenseId.Value, m.ShareId.Value,
                m.FromUserId.Value, m.ToUserId.Value, m.Amount.Amount, m.Amount.Currency,
                m.OccurrenceDate, m.ValueDate, ct);
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<SettlementReversed> context) =>
        HandleAsync(context.Message, nameof(SettlementReversed), ct =>
        {
            var m = context.Message;
            return _bookkeeping.ReverseBySourceAsync(
                m.GroupId.Value,
                LedgerSources.Settlement(m.ExpenseId.Value, m.OccurrenceDate, m.FromUserId.Value), ct);
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<VendorPaid> context) =>
        HandleAsync(context.Message, nameof(VendorPaid), ct =>
        {
            var m = context.Message;
            return _bookkeeping.RecordVendorPaymentFromEventAsync(
                m.ExpenseId.Value, m.FundingSource, m.PaidByUserId, m.OccurrenceDate, m.PaidAt.Date, ct);
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<VendorPaymentReversed> context) =>
        HandleAsync(context.Message, nameof(VendorPaymentReversed), ct =>
        {
            var m = context.Message;
            return _bookkeeping.ReverseVendorPaymentFromEventAsync(m.ExpenseId.Value, m.OccurrenceDate, ct);
        }, context.CancellationToken);

    // Bookkeeping commits BEFORE the processed marker is saved — a crash in between redelivers the
    // event, and the convergent handlers no-op.
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
            // A concurrent writer already posted this entry under the same (ledger, source) and the partial
            // unique index rejected our duplicate. The books already hold it, so this delivery is a no-op. We
            // return without recording the marker (the rejected insert is still tracked on this scoped
            // context); the message is ACKed.
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

// One queue, one message at a time: every ledger journalLine serializes through this endpoint, so
// reverse-then-repost sequences from different events can never interleave into duplicate or
// missing lines. Throughput is bounded by ledger volume, which is human-scale here.
internal sealed class LedgerJournalLineConsumerDefinition : ConsumerDefinition<LedgerJournalLineConsumer>
{
    public LedgerJournalLineConsumerDefinition()
    {
        ConcurrentMessageLimit = 1;
    }
}
