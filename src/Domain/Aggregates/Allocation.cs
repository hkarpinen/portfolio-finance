using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

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

    // The accounting lives in the ledger; this raises the fact for the occurrence and changes no
    // state. Deliberately a PURE event: the allocation stays a source document rather than becoming a
    // second store of settled-state, which would be a dual write.
    public void Settle(UserId toUserId, DateTime occurrence, DateTime valueDate)
    {
        _domainEvents.Add(new SettlementRecorded(
            Id, ChargeId, GroupId, UserId, toUserId, Amount,
            DateTime.SpecifyKind(occurrence.Date, DateTimeKind.Utc),
            DateTime.SpecifyKind(valueDate.Date, DateTimeKind.Utc)));
    }

    // Raises the fact; the ledger is reversed separately. No state change.
    public void ReverseSettlement(DateTime occurrence)
    {
        _domainEvents.Add(new SettlementReversed(
            Id, ChargeId, GroupId, UserId,
            DateTime.SpecifyKind(occurrence.Date, DateTimeKind.Utc)));
    }
}
