using Bills.Domain.Events;
using Bills.Domain.ValueObjects;

namespace Bills.Domain.Aggregates;

/// <summary>
/// Personal bill aggregate root representing an individual user's personal expense.
/// Unlike household bills, these are owned by a single user and not shared.
/// </summary>
public class PersonalBill
{
    private readonly List<DomainEvent> _domainEvents = new();

    public PersonalBillId Id { get; private set; }
    public UserId UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Money Amount { get; private set; }
    public BillCategory Category { get; private set; }
    public DateTime DueDate { get; private set; }
    public RecurrenceSchedule? RecurrenceSchedule { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    private PersonalBill()
    {
    }

    public static PersonalBill Create(
        UserId userId,
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

        var bill = new PersonalBill
        {
            Id = PersonalBillId.New(),
            UserId = userId,
            Title = title,
            Description = description,
            Amount = amount,
            Category = category,
            DueDate = DateTime.SpecifyKind(dueDate, DateTimeKind.Utc),
            RecurrenceSchedule = recurrenceSchedule,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true,
        };

        bill._domainEvents.Add(new PersonalBillCreated(
            bill.Id,
            userId,
            title,
            amount,
            category,
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
        DueDate = DateTime.SpecifyKind(dueDate, DateTimeKind.Utc);
        RecurrenceSchedule = recurrenceSchedule;
        Description = description;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new PersonalBillUpdated(Id, title, amount, category, dueDate));
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new InvalidOperationException("Personal bill is already inactive.");

        IsActive = false;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new PersonalBillDeactivated(Id));
    }

    public bool TryDeactivate()
    {
        if (!IsActive) return false;
        Deactivate();
        return true;
    }
}
