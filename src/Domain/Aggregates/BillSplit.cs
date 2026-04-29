using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// Bill split aggregate root representing a member's share of a bill.
/// </summary>
public class BillSplit
{
    private readonly List<DomainEvent> _domainEvents = new();

    public SplitId Id { get; private set; }
    public BillId BillId { get; private set; }
    public HouseholdId HouseholdId { get; private set; }
    public MembershipId MembershipId { get; private set; }
    public UserId UserId { get; private set; }
    public Money Amount { get; private set; }
    public bool IsClaimed { get; private set; }
    public DateTime? ClaimedAt { get; private set; }
    public UserId? ClaimedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    private BillSplit()
    {
    }

    public static BillSplit Create(
        BillId billId,
        HouseholdId householdId,
        MembershipId membershipId,
        UserId userId,
        Money amount)
    {
        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        var split = new BillSplit
        {
            Id = SplitId.New(),
            BillId = billId,
            HouseholdId = householdId,
            MembershipId = membershipId,
            UserId = userId,
            Amount = amount,
            IsClaimed = false,
            ClaimedAt = null,
            ClaimedBy = null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        split._domainEvents.Add(new BillSplitCreated(split.Id, billId, householdId, membershipId, userId, amount));

        return split;
    }

    public void Claim(UserId claimedBy)
    {
        if (IsClaimed)
            throw new InvalidOperationException("Bill split is already claimed.");

        IsClaimed = true;
        ClaimedAt = DateTime.UtcNow;
        ClaimedBy = claimedBy;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new BillSplitClaimed(Id, BillId, HouseholdId, MembershipId, Amount));
    }

    public void Update(Money newAmount)
    {
        if (newAmount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(newAmount));

        Amount = newAmount;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new BillSplitUpdated(Id, BillId, HouseholdId, newAmount));
    }

    public void Remove()
    {
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new BillSplitRemoved(Id, BillId, HouseholdId, MembershipId));
    }
}
