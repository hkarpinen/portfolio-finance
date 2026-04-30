using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// Bill aggregate root representing an expense shared among household members.
/// </summary>
public class Bill
{
    private readonly List<DomainEvent> _domainEvents = new();

    public BillId Id { get; private set; }
    public HouseholdId HouseholdId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Money Amount { get; private set; }
    public BillCategory Category { get; private set; }
    public UserId CreatedBy { get; private set; }
    public DateTime DueDate { get; private set; }
    public RecurrenceSchedule? RecurrenceSchedule { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    private Bill()
    {
    }

    public static Bill Create(
        HouseholdId householdId,
        string title,
        Money amount,
        BillCategory category,
        UserId createdBy,
        DateTime dueDate,
        RecurrenceSchedule? recurrenceSchedule = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        var bill = new Bill
        {
            Id = BillId.New(),
            HouseholdId = householdId,
            Title = title,
            Description = description,
            Amount = amount,
            Category = category,
            CreatedBy = createdBy,
            DueDate = dueDate,
            RecurrenceSchedule = recurrenceSchedule,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        bill._domainEvents.Add(new BillCreated(
            bill.Id,
            householdId,
            title,
            amount,
            category,
            createdBy,
            dueDate,
            recurrenceSchedule));

        return bill;
    }

    public void Update(
        string title,
        Money amount,
        BillCategory category,
        DateTime dueDate,
        RecurrenceSchedule? recurrenceSchedule = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));

        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        Title = title;
        Amount = amount;
        Category = category;
        DueDate = dueDate;
        RecurrenceSchedule = recurrenceSchedule;
        Description = description;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new BillUpdated(Id, HouseholdId, title, amount, category, dueDate));
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new InvalidOperationException("Bill is already inactive.");

        IsActive = false;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new BillDeactivated(Id, HouseholdId));
    }

    public void Activate()
    {
        if (IsActive)
            throw new InvalidOperationException("Bill is already active.");

        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
