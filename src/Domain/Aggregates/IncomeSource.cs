using Bills.Domain.Events;
using Bills.Domain.ValueObjects;

namespace Bills.Domain.Aggregates;

/// <summary>
/// Income source aggregate root representing a member's income contribution to the household.
/// </summary>
public class IncomeSource
{
    private readonly List<DomainEvent> _domainEvents = new();

    public IncomeId Id { get; private set; }
    /// <summary>Nullable — income sources can exist independently of a household.</summary>
    public HouseholdId? HouseholdId { get; private set; }
    /// <summary>Nullable — only set when linked to a household membership.</summary>
    public MembershipId? MembershipId { get; private set; }
    public UserId UserId { get; private set; }
    public Money Amount { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public RecurrenceSchedule RecurrenceSchedule { get; private set; } = null!;
    /// <summary>Optional: the date the user last received this income. Used for per-month projection calculations.</summary>
    public DateTime? LastPaymentDate { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    private IncomeSource()
    {
    }

    public static IncomeSource Create(
        UserId userId,
        Money amount,
        string source,
        RecurrenceSchedule recurrenceSchedule,
        HouseholdId? householdId = null,
        MembershipId? membershipId = null,
        DateTime? lastPaymentDate = null)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source cannot be empty.", nameof(source));

        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        var incomeSource = new IncomeSource
        {
            Id = IncomeId.New(),
            HouseholdId = householdId,
            MembershipId = membershipId,
            UserId = userId,
            Amount = amount,
            Source = source,
            RecurrenceSchedule = recurrenceSchedule,
            LastPaymentDate = lastPaymentDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        incomeSource._domainEvents.Add(new IncomeSourceCreated(
            incomeSource.Id,
            householdId,
            membershipId,
            userId,
            amount,
            source,
            recurrenceSchedule));

        return incomeSource;
    }

    public void Update(Money amount, string source, RecurrenceSchedule recurrenceSchedule, DateTime? lastPaymentDate = null)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source cannot be empty.", nameof(source));

        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        Amount = amount;
        Source = source;
        RecurrenceSchedule = recurrenceSchedule;
        if (lastPaymentDate.HasValue)
            LastPaymentDate = lastPaymentDate;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new IncomeSourceUpdated(Id, HouseholdId, amount, source));
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new InvalidOperationException("Income source is already inactive.");

        IsActive = false;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new IncomeSourceDeactivated(Id, HouseholdId));
    }

    /// <summary>
    /// Deactivates the income source. Returns false (without throwing) if already inactive.
    /// </summary>
    public bool TryDeactivate()
    {
        if (!IsActive) return false;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new IncomeSourceDeactivated(Id, HouseholdId));
        return true;
    }

    public void Activate()
    {
        if (IsActive)
            throw new InvalidOperationException("Income source is already active.");

        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
