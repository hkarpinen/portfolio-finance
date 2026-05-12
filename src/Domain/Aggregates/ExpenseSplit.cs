using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// ExpenseSplit aggregate root representing a member's share of a household expense.
/// </summary>
public class ExpenseSplit : IAggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public ExpenseSplitId Id { get; private set; }
    public ExpenseId ExpenseId { get; private set; }
    public HouseholdId HouseholdId { get; private set; }
    public MembershipId MembershipId { get; private set; }
    public UserId UserId { get; private set; }
    public Money Amount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private ExpenseSplit()
    {
    }

    public static ExpenseSplit Create(
        ExpenseId expenseId,
        HouseholdId householdId,
        MembershipId membershipId,
        UserId userId,
        Money amount)
    {
        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        var split = new ExpenseSplit
        {
            Id = ExpenseSplitId.New(),
            ExpenseId = expenseId,
            HouseholdId = householdId,
            MembershipId = membershipId,
            UserId = userId,
            Amount = amount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        split._domainEvents.Add(new ExpenseSplitCreated(split.Id, expenseId, householdId, membershipId, userId, amount));
        return split;
    }

    public void Update(Money newAmount)
    {
        if (newAmount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(newAmount));

        Amount = newAmount;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new ExpenseSplitUpdated(Id, ExpenseId, HouseholdId, newAmount));
    }

    public void Remove()
    {
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new ExpenseSplitRemoved(Id, ExpenseId, HouseholdId, MembershipId));
    }
}
