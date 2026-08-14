using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

public class Allocation : IAggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public AllocationId Id { get; private set; }
    public ChargeId ChargeId { get; private set; }
    public UserId UserId { get; private set; }
    public Money Amount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private Allocation()
    {
    }

    /// <summary>
    /// A share of one charge. The charge itself is passed rather than its id and group, because an
    /// allocation's group is the charge's group — storing a second copy let the two disagree, and a
    /// caller-supplied group is a guess the charge can settle.
    /// </summary>
    public static Allocation Create(Charge charge, UserId userId, Money amount)
    {
        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));
        if (charge.GroupId is null)
            throw new InvalidOperationException("A personal charge is nobody else's share to carry.");

        var allocation = new Allocation
        {
            Id = AllocationId.New(),
            ChargeId = charge.Id,
            UserId = userId,
            Amount = amount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        allocation._domainEvents.Add(
            new AllocationCreated(allocation.Id, charge.Id, charge.GroupId.Value, userId, amount));
        return allocation;
    }

    public void Update(Charge charge, Money newAmount)
    {
        if (newAmount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(newAmount));

        var groupId = GroupOf(charge);
        Amount = newAmount;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new AllocationUpdated(Id, ChargeId, groupId, newAmount));
    }

    public void Remove(Charge charge)
    {
        var groupId = GroupOf(charge);
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new AllocationRemoved(Id, ChargeId, groupId));
    }

    /// <summary>
    /// The group the events carry. They still name it because a reversal may arrive after the
    /// allocation is gone, and by then there is nothing left to derive it from.
    /// </summary>
    private GroupId GroupOf(Charge charge)
    {
        if (charge.Id != ChargeId)
            throw new InvalidOperationException("That charge is not the one this share is on.");
        if (charge.GroupId is null)
            throw new InvalidOperationException("A personal charge is nobody else's share to carry.");
        return charge.GroupId.Value;
    }

    // The accounting lives in the ledger; this raises the fact for the occurrence and changes no
    // state. Deliberately a PURE event: the allocation stays a source document rather than becoming a
    // second store of settled-state, which would be a dual write.
    public void Settle(Charge charge, UserId toUserId, DateTime occurrence, DateTime valueDate)
    {
        _domainEvents.Add(new SettlementRecorded(
            Id, ChargeId, GroupOf(charge), UserId, toUserId, Amount,
            DateTime.SpecifyKind(occurrence.Date, DateTimeKind.Utc),
            DateTime.SpecifyKind(valueDate.Date, DateTimeKind.Utc)));
    }

    // Raises the fact; the ledger is reversed separately. No state change.
    public void ReverseSettlement(Charge charge, DateTime occurrence)
    {
        _domainEvents.Add(new SettlementReversed(
            Id, ChargeId, GroupOf(charge), UserId,
            DateTime.SpecifyKind(occurrence.Date, DateTimeKind.Utc)));
    }
}
