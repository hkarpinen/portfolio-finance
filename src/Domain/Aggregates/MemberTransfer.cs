using Finance.Domain.Engines;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// One member handing money straight to another to square up — no expense involved, no pot.
///
/// It exists because the books cannot be posted to from an HTTP handler. Every other mutation here
/// commits a document and lets the outbox drive the posting; settling up had no document, so the
/// controller called the bookkeeper itself. That also left "not yourself" and "a positive amount"
/// with nowhere to live but the controller, where anything arriving another way missed them.
///
/// The document is the fact that somebody paid. What that means in debits and credits is decided
/// later, by whoever consumes it.
/// </summary>
public sealed class MemberTransfer : IAggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public MemberTransferId Id { get; private set; }
    public GroupId GroupId { get; private set; }
    public UserId FromUserId { get; private set; }
    public UserId ToUserId { get; private set; }
    public Money Amount { get; private set; }
    public DateTime OccurredOn { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private MemberTransfer() { }

    public static MemberTransfer Record(
        GroupId groupId, UserId fromUserId, UserId toUserId, Money amount, DateTime occurredOn)
    {
        // Both legs would land on ONE member account: it nets to nothing, satisfies double-entry,
        // and records a payment that never happened.
        if (fromUserId == toUserId)
            throw new InvalidOperationException("Nobody settles up with themselves.");
        if (amount.Amount <= 0m)
            throw new ArgumentException("A settle-up moves a positive amount.", nameof(amount));

        var transfer = new MemberTransfer
        {
            Id = MemberTransferId.New(),
            GroupId = groupId,
            FromUserId = fromUserId,
            ToUserId = toUserId,
            Amount = amount,
            OccurredOn = DateTime.SpecifyKind(occurredOn.Date, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow,
        };

        transfer._domainEvents.Add(new MemberTransferRecorded(
            transfer.Id, groupId, fromUserId, toUserId, amount, transfer.OccurredOn));
        return transfer;
    }

    /// <summary>
    /// The journal entry's key. Its own id, not a composite of who and when: settle-ups repeat
    /// between the same two people, so anything derived from the pair would make the second payment
    /// look like a redelivery of the first and swallow it.
    /// </summary>
    public string LedgerSource => LedgerSources.SettleUp(Id.Value);
}
