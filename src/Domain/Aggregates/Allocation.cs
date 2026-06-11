using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// Allocation aggregate root representing a member's share of a household expense.
/// </summary>
public class Allocation : IAggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public AllocationId Id { get; private set; }
    public ChargeId ChargeId { get; private set; }
    public GroupId GroupId { get; private set; }
    public UserId UserId { get; private set; }
    public Money Amount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private Allocation()
    {
    }

    public static Allocation Create(
        ChargeId chargeId,
        GroupId groupId,
        UserId userId,
        Money amount)
    {
        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        var allocation = new Allocation
        {
            Id = AllocationId.New(),
            ChargeId = chargeId,
            GroupId = groupId,
            UserId = userId,
            Amount = amount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        allocation._domainEvents.Add(new AllocationCreated(allocation.Id, chargeId, groupId, userId, amount));
        return allocation;
    }

    public void Update(Money newAmount)
    {
        if (newAmount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(newAmount));

        Amount = newAmount;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new AllocationUpdated(Id, ChargeId, GroupId, newAmount));
    }

    public void Remove()
    {
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new AllocationRemoved(Id, ChargeId, GroupId));
    }

    /// <summary>
    /// The member (this allocation's <see cref="UserId"/>) settles their share into the funding
    /// account that fronted the bill (<paramref name="toUserId"/>). The accounting lives in the
    /// ledger (the single source of truth); this aggregate only raises the cross-service fact for
    /// the occurrence. It is a <em>pure event</em> — no state change — so the allocation stays a
    /// source document and is not a second settled-state store (that would re-introduce the
    /// dual-write the ledger collapse removed).
    /// </summary>
    public void Settle(UserId toUserId, DateTime occurrence, DateTime valueDate)
    {
        _domainEvents.Add(new SettlementRecorded(
            Id, ChargeId, GroupId, UserId, toUserId, Amount,
            DateTime.SpecifyKind(occurrence.Date, DateTimeKind.Utc),
            DateTime.SpecifyKind(valueDate.Date, DateTimeKind.Utc)));
    }

    /// <summary>The member un-settles their share for the occurrence (the ledger is reversed
    /// separately). Raises the cross-service fact; no state change.</summary>
    public void ReverseSettlement(DateTime occurrence)
    {
        _domainEvents.Add(new SettlementReversed(
            Id, ChargeId, GroupId, UserId,
            DateTime.SpecifyKind(occurrence.Date, DateTimeKind.Utc)));
    }
}
