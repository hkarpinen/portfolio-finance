using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// Expense aggregate root representing either a personal expense (HouseholdId is
/// null) or a household shared expense (HouseholdId is set, CreatedBy is the author).
/// </summary>
public class Expense : IAggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public ExpenseId Id { get; private set; }
    public UserId UserId { get; private set; }

    /// <summary>Non-null for group (shared) expenses; null for personal expenses.</summary>
    public GroupId? GroupId { get; private set; }

    /// <summary>The member who created the household expense. Null for personal expenses.</summary>
    public UserId? CreatedBy { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Money Amount { get; private set; }
    public ExpenseCategory Category { get; private set; }
    public DateTime DueDate { get; private set; }
    public RecurrenceSchedule? RecurrenceSchedule { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private Expense()
    {
    }

    /// <summary>Creates a personal expense (HouseholdId = null).</summary>
    public static Expense Create(
        UserId userId,
        string title,
        Money amount,
        ExpenseCategory category,
        DateTime dueDate,
        RecurrenceSchedule? recurrenceSchedule = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        var expense = new Expense
        {
            Id = ExpenseId.New(),
            UserId = userId,
            GroupId = null,
            CreatedBy = null,
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

        expense._domainEvents.Add(new ExpenseCreated(
            expense.Id,
            userId,
            title,
            amount,
            category,
            dueDate,
            recurrenceSchedule));

        return expense;
    }

    /// <summary>Creates a group shared expense (GroupId set, UserId = createdBy).</summary>
    public static Expense CreateHousehold(
        GroupId groupId,
        UserId createdBy,
        string title,
        Money amount,
        ExpenseCategory category,
        DateTime dueDate,
        RecurrenceSchedule? recurrenceSchedule = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        var expense = new Expense
        {
            Id = ExpenseId.New(),
            UserId = createdBy,
            GroupId = groupId,
            CreatedBy = createdBy,
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

        expense._domainEvents.Add(new ExpenseCreated(
            expense.Id,
            createdBy,
            title,
            amount,
            category,
            dueDate,
            recurrenceSchedule,
            groupId));

        return expense;
    }

    public void Update(
        string title,
        Money amount,
        ExpenseCategory category,
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

        _domainEvents.Add(new ExpenseUpdated(Id, title, amount, category, dueDate, GroupId));
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new InvalidOperationException("Expense is already inactive.");

        IsActive = false;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new ExpenseDeactivated(Id, GroupId));
    }

    public void Activate()
    {
        if (IsActive)
            throw new InvalidOperationException("Expense is already active.");

        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool TryDeactivate()
    {
        if (!IsActive) return false;
        Deactivate();
        return true;
    }
}
