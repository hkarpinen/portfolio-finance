using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// The standing agreement behind a repeating cost — rent, a subscription, the internet bill.
///
/// It says which <see cref="Charge"/>s SHOULD exist and when: an anchor date stepped by an
/// interval. It never posts anything itself. A charge only becomes real when somebody acts on
/// that occurrence, and it takes the amount and split this schedule held AT THAT MOMENT.
///
/// That copy is the whole point. Editing a schedule changes what has not happened yet and cannot
/// reach back into a month already recorded — which is what stopped past months moving when the
/// rent went up.
/// </summary>
public sealed class ChargeSchedule : IAggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public ChargeScheduleId Id { get; private set; }
    public UserId UserId { get; private set; }

    /// <summary>Null is personal, set is shared — same discriminator <see cref="Charge"/> uses.</summary>
    public GroupId? GroupId { get; private set; }

    public UserId? CreatedBy { get; private set; }
    public Guid? PayerUserId { get; private set; }
    public FundingSource FundingSource { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    /// <summary>What the NEXT generated charge will cost. Charges already generated keep their own.</summary>
    public Money Amount { get; private set; }

    public ChargeCategory Category { get; private set; }
    public RecurrenceSchedule Recurrence { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private ChargeSchedule() { }

    public static ChargeSchedule Create(
        UserId userId,
        GroupId? groupId,
        string title,
        Money amount,
        ChargeCategory category,
        RecurrenceSchedule recurrence,
        string? description = null,
        UserId? createdBy = null,
        Guid? payerUserId = null,
        FundingSource fundingSource = FundingSource.PayerMember)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        var now = DateTime.UtcNow;
        var schedule = new ChargeSchedule
        {
            Id = ChargeScheduleId.New(),
            UserId = userId,
            GroupId = groupId,
            CreatedBy = createdBy,
            PayerUserId = payerUserId,
            FundingSource = fundingSource,
            Title = title,
            Description = description,
            Amount = amount,
            Category = category,
            Recurrence = recurrence,
            CreatedAt = now,
            UpdatedAt = now,
            IsActive = true,
        };
        schedule._domainEvents.Add(new ChargeScheduleCreated(
            schedule.Id.Value, userId.Value, groupId?.Value, title, amount.Amount, amount.Currency,
            category.ToString(), recurrence.Frequency.ToString(), recurrence.StartDate, now));
        return schedule;
    }

    /// <summary>Takes effect on occurrences not yet generated. Nothing already recorded moves.</summary>
    public void Amend(string title, Money amount, ChargeCategory category, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        Title = title;
        Amount = amount;
        Category = category;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new ChargeScheduleAmended(
            Id.Value, title, amount.Amount, amount.Currency, category.ToString(), UpdatedAt));
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new ChargeScheduleDeactivated(Id.Value, UpdatedAt));
    }

    /// <summary>
    /// The dates this schedule says a charge belongs on, within a window. Nothing is stored — the
    /// forecast every screen draws is this, and a charge exists for a date only once acted on.
    /// </summary>
    public IReadOnlyList<DateTime> OccurrencesIn(DateTime from, DateTime toExclusive) =>
        IsActive ? Recurrence.GetOccurrencesInRange(from, toExclusive) : [];

    /// <summary>
    /// True when this schedule really does place a charge on that date. Guards generation: a
    /// caller cannot invent an occurrence the agreement never described.
    /// </summary>
    public bool Covers(DateTime occurrenceDate)
    {
        var day = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc);
        return OccurrencesIn(day, day.AddDays(1)).Count == 1;
    }
}
