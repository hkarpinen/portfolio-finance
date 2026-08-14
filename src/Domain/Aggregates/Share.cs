using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

public class Share : IAggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public ShareId Id { get; private set; }
    public ExpenseId ExpenseId { get; private set; }
    public UserId UserId { get; private set; }
    public Money Amount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private Share()
    {
    }

    /// <summary>
    /// A share of one expense. The expense itself is passed rather than its id and group, because an
    /// share's group is the expense's group — storing a second copy let the two disagree, and a
    /// caller-supplied group is a guess the expense can settle.
    /// </summary>
    public static Share Create(Expense expense, UserId userId, Money amount)
    {
        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));
        if (expense.GroupId is null)
            throw new InvalidOperationException("A personal expense is nobody else's share to carry.");

        var share = new Share
        {
            Id = ShareId.New(),
            ExpenseId = expense.Id,
            UserId = userId,
            Amount = amount,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        share._domainEvents.Add(
            new ShareCreated(share.Id, expense.Id, expense.GroupId.Value, userId, amount));
        return share;
    }

    public void Update(Expense expense, Money newAmount)
    {
        if (newAmount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(newAmount));

        var groupId = GroupOf(expense);
        Amount = newAmount;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new ShareUpdated(Id, ExpenseId, groupId, newAmount));
    }

    public void Remove(Expense expense)
    {
        var groupId = GroupOf(expense);
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new ShareRemoved(Id, ExpenseId, groupId));
    }

    /// <summary>
    /// The group the events carry. They still name it because a reversal may arrive after the
    /// share is gone, and by then there is nothing left to derive it from.
    /// </summary>
    private GroupId GroupOf(Expense expense)
    {
        if (expense.Id != ExpenseId)
            throw new InvalidOperationException("That expense is not the one this share is on.");
        if (expense.GroupId is null)
            throw new InvalidOperationException("A personal expense is nobody else's share to carry.");
        return expense.GroupId.Value;
    }

    // The accounting lives in the ledger; this raises the fact for the occurrence and changes no
    // state. Deliberately a PURE event: the share stays a source document rather than becoming a
    // second store of settled-state, which would be a dual write.
    public void Settle(Expense expense, UserId toUserId, DateTime occurrence, DateTime valueDate)
    {
        _domainEvents.Add(new SettlementRecorded(
            Id, ExpenseId, GroupOf(expense), UserId, toUserId, Amount,
            DateTime.SpecifyKind(occurrence.Date, DateTimeKind.Utc),
            DateTime.SpecifyKind(valueDate.Date, DateTimeKind.Utc)));
    }

    // Raises the fact; the ledger is reversed separately. No state change.
    public void ReverseSettlement(Expense expense, DateTime occurrence)
    {
        _domainEvents.Add(new SettlementReversed(
            Id, ExpenseId, GroupOf(expense), UserId,
            DateTime.SpecifyKind(occurrence.Date, DateTimeKind.Utc)));
    }
}
