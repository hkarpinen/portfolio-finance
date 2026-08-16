using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// The standing agreement behind a repeating cost — rent, a subscription, the internet.
///
/// It says which <see cref="Expense"/>s SHOULD exist and when: an anchor date stepped by an
/// interval. It never posts anything itself. An expense only becomes real when somebody acts on
/// that occurrence, and it takes the amount and split this schedule held AT THAT MOMENT.
///
/// That copy is the whole point. Editing a schedule changes what has not happened yet and cannot
/// reach back into a month already recorded — which is what stopped past months moving when the
/// rent went up.
/// </summary>
public sealed class RecurringExpense : IAggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public RecurringExpenseId Id { get; private set; }

    /// <summary>Whose books the expenses it generates will land in — the same entity
    /// <see cref="Expense"/> carries.</summary>
    public AccountingEntity Owner { get; private set; }

    public GroupId? GroupId => Owner.AsGroup;

    /// <summary>
    /// Who opened the agreement. On a personal schedule that is also whose it is; on a shared one
    /// the group holds the agreement and this names whoever set it up. One field for both because
    /// the two were always written with the same value.
    /// </summary>
    public UserId CreatedBy { get; private set; }
    public Guid? PayerUserId { get; private set; }
    public FundingSource FundingSource { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    private readonly List<RecurringExpenseTerm> _amounts = new();

    /// <summary>
    /// Every amount this schedule has held, each from the date it took effect. A rise is a new
    /// version rather than an edit, so generating an expense for a month in the past uses what was
    /// true then — not today's figure.
    /// </summary>
    public IReadOnlyList<RecurringExpenseTerm> Amounts => _amounts.AsReadOnly();

    /// <summary>Fixed at creation — an agreement does not change currency halfway through.</summary>
    public string Currency { get; private set; } = "USD";

    /// <summary>The amount in force today. Shorthand for <see cref="AmountOn"/> with today's date.</summary>
    public Money Amount => AmountOn(DateTime.UtcNow);

    public ExpenseCategory Category { get; private set; }
    public RecurrenceSchedule Recurrence { get; private set; } = null!;

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private RecurringExpense() { }

    public static RecurringExpense Create(
        AccountingEntity owner,
        UserId createdBy,
        string title,
        Money amount,
        ExpenseCategory category,
        RecurrenceSchedule recurrence,
        string? description = null,
        Guid? payerUserId = null,
        FundingSource fundingSource = FundingSource.PayerMember)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        var now = DateTime.UtcNow;
        if (owner.IsPerson && !owner.Is(createdBy.Value))
            throw new InvalidOperationException("Nobody opens an agreement in somebody else's own book.");

        var schedule = new RecurringExpense
        {
            Id = RecurringExpenseId.New(),
            Owner = owner,
            CreatedBy = createdBy,
            PayerUserId = payerUserId,
            FundingSource = fundingSource,
            Title = title,
            Description = description,
            Category = category,
            Currency = amount.Currency,
            Recurrence = recurrence,
            CreatedAt = now,
            UpdatedAt = now,
            IsActive = true,
        };
        // The first version runs from the anchor, so an occurrence on day one has an amount.
        schedule._amounts.Add(RecurringExpenseTerm.From(recurrence.StartDate, amount.Amount));
        schedule._domainEvents.Add(new RecurringExpenseCreated(
            schedule.Id.Value, createdBy.Value, schedule.GroupId?.Value, title, amount.Amount, amount.Currency,
            category.ToString(), recurrence.Frequency.ToString(), recurrence.StartDate, now));
        return schedule;
    }

    /// <summary>
    /// A new amount from <paramref name="effectiveFrom"/> onward — the old one stays on record and
    /// still governs occurrences before that date. Expenses already generated are untouched either
    /// way; they froze their amount when they were written.
    ///
    /// Re-amending the same effective date replaces that version rather than stacking a second one,
    /// so correcting a typo does not leave two answers for one day.
    /// </summary>
    public void Amend(
        string title, Money amount, ExpenseCategory category, string? description,
        DateTime? effectiveFrom = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        if (amount.Currency != Currency)
            throw new InvalidOperationException(
                $"{Title} is agreed in {Currency}; an amendment cannot change that.");

        var from = RecurringExpenseTerm.From(effectiveFrom ?? DateTime.UtcNow, amount.Amount);
        _amounts.RemoveAll(a => a.EffectiveFrom == from.EffectiveFrom);
        _amounts.Add(from);
        _amounts.Sort((a, b) => a.EffectiveFrom.CompareTo(b.EffectiveFrom));

        Title = title;
        Category = category;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new RecurringExpenseAmended(
            Id.Value, title, amount.Amount, amount.Currency, category.ToString(), UpdatedAt));
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new RecurringExpenseDeactivated(Id.Value, UpdatedAt));
    }

    /// <summary>
    /// Whether this person may change the agreement — amend its terms or stop it.
    ///
    /// Whoever opened it, matching the rule <see cref="Expense.IsManagedBy"/> already enforces on
    /// the expenses it generates. Membership is not enough: being in the group lets you see what the
    /// rent agreement says, not raise it.
    /// </summary>
    public bool IsManagedBy(Guid userId) => CreatedBy.Value == userId;

    /// <summary>
    /// Whether this person may read the agreement and its forecast. A personal schedule is its
    /// owner's alone; a group's is open to current members, which the caller establishes —
    /// membership is a database question and cannot be answered from in here.
    /// </summary>
    public bool IsVisibleTo(Guid userId, bool isMember) =>
        Owner.IsGroup ? isMember : Owner.Is(userId);

    /// <summary>
    /// What this schedule held on a given date — the latest version in force by then. Before
    /// the first version (a back-dated occurrence) the earliest one applies, because that is the
    /// only figure anybody ever agreed.
    /// </summary>
    public Money AmountOn(DateTime date)
    {
        var day = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var inForce = _amounts.LastOrDefault(a => a.EffectiveFrom <= day) ?? _amounts.FirstOrDefault();
        if (inForce is null)
            throw new InvalidOperationException($"{Title} has no amount on record.");

        return Money.Create(inForce.Amount, Currency);
    }

    /// <summary>
    /// The dates this schedule says an expense belongs on, within a window. Nothing is stored — the
    /// forecast every screen draws is this, and an expense exists for a date only once acted on.
    /// </summary>
    public IReadOnlyList<DateTime> OccurrencesIn(DateTime from, DateTime toExclusive) =>
        IsActive ? Recurrence.GetOccurrencesInRange(from, toExclusive) : [];

    /// <summary>
    /// True when this schedule really does place an expense on that date. Guards generation: a
    /// caller cannot invent an occurrence the agreement never described.
    /// </summary>
    public bool Covers(DateTime occurrenceDate)
    {
        var day = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc);
        return OccurrencesIn(day, day.AddDays(1)).Count == 1;
    }
}
