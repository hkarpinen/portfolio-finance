using Finance.Domain.Engines;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// Income source aggregate root representing a member's income contribution to the household.
/// </summary>
public class IncomeSource : IAggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public IncomeId Id { get; private set; }
    public UserId UserId { get; private set; }
    public Money Amount { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public RecurrenceSchedule RecurrenceSchedule { get; private set; } = null!;
    /// <summary>
    /// How often a paycheck actually arrives (the payment cadence).
    /// May differ from RecurrenceSchedule.Frequency, which represents
    /// the period the <see cref="Amount"/> is quoted in (e.g. monthly salary paid bi-weekly).
    /// Defaults to RecurrenceSchedule.Frequency when not specified.
    /// </summary>
    public RecurrenceFrequency PaymentFrequency { get; private set; }
    /// <summary>The date of the most recent paycheck — used as the recurrence anchor
    /// so the schedule generates exact real-world pay dates.</summary>
    public DateTime? LastPaymentDate { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>Optional free-form notes about the source (e.g. employer, job title). ≤ 500 chars.</summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// Optional tax withholding profile used by the PayrollDeductionEngine to estimate
    /// federal income tax, state income tax, and FICA deductions.
    /// Null means no tax estimation is performed for this source.
    /// </summary>
    public TaxWithholdingProfile? TaxProfile { get; private set; }

    /// <summary>
    /// Voluntary payroll deductions (health, dental, retirement, etc.).
    /// Tax deductions are NOT stored here — they are engine-computed from <see cref="TaxProfile"/>.
    /// </summary>
    private readonly List<PayrollDeduction> _deductions = new();
    public IReadOnlyList<PayrollDeduction> Deductions => _deductions.AsReadOnly();

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
        RecurrenceFrequency? paymentFrequency = null,
        DateTime? lastPaymentDate = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source cannot be empty.", nameof(source));

        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        if (notes?.Length > 500)
            throw new ArgumentException("Notes must be ≤ 500 characters.", nameof(notes));

        var incomeSource = new IncomeSource
        {
            Id = IncomeId.New(),
            UserId = userId,
            Amount = amount,
            Source = source,
            RecurrenceSchedule = recurrenceSchedule,
            PaymentFrequency = paymentFrequency ?? recurrenceSchedule.Frequency,
            LastPaymentDate = lastPaymentDate.HasValue ? DateTime.SpecifyKind(lastPaymentDate.Value, DateTimeKind.Utc) : null,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };

        incomeSource._domainEvents.Add(new IncomeSourceCreated(
            incomeSource.Id,
            userId,
            amount,
            source,
            recurrenceSchedule));

        return incomeSource;
    }

    public void Update(Money amount, string source, RecurrenceSchedule recurrenceSchedule, RecurrenceFrequency? paymentFrequency = null, DateTime? lastPaymentDate = null, string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source cannot be empty.", nameof(source));

        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        if (notes?.Length > 500)
            throw new ArgumentException("Notes must be ≤ 500 characters.", nameof(notes));

        Notes = notes;

        Amount = amount;
        Source = source;
        RecurrenceSchedule = recurrenceSchedule;
        PaymentFrequency = paymentFrequency ?? recurrenceSchedule.Frequency;
        if (lastPaymentDate.HasValue)
            LastPaymentDate = DateTime.SpecifyKind(lastPaymentDate.Value, DateTimeKind.Utc);
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new IncomeSourceUpdated(Id, amount, source));
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new InvalidOperationException("Income source is already inactive.");

        IsActive = false;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new IncomeSourceDeactivated(Id));
    }

    /// <summary>
    /// Deactivates the income source. Returns false (without throwing) if already inactive.
    /// </summary>
    public bool TryDeactivate()
    {
        if (!IsActive) return false;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new IncomeSourceDeactivated(Id));
        return true;
    }

    public void Activate()
    {
        if (IsActive)
            throw new InvalidOperationException("Income source is already active.");

        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new IncomeSourceActivated(Id));
    }

    // ── Tax profile ──────────────────────────────────────────────────────────

    public void SetTaxProfile(TaxWithholdingProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        TaxProfile = profile;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new IncomeSourceTaxProfileSet(Id, profile));
    }

    public void ClearTaxProfile()
    {
        if (TaxProfile is null) return;
        TaxProfile = null;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new IncomeSourceTaxProfileSet(Id, null));
    }

    // ── Voluntary deductions ─────────────────────────────────────────────────

    public void AddDeduction(PayrollDeduction deduction)
    {
        ArgumentNullException.ThrowIfNull(deduction);

        var existing = _deductions.FirstOrDefault(d =>
            d.Type == deduction.Type &&
            string.Equals(d.Label, deduction.Label, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
            throw new InvalidOperationException(
                $"A deduction of type '{deduction.Type}' with label '{deduction.Label}' already exists.");

        _deductions.Add(deduction);
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new IncomeSourceDeductionAdded(Id, deduction));
    }

    public void RemoveDeduction(DeductionType type, string label)
    {
        var existing = _deductions.FirstOrDefault(d =>
            d.Type == type &&
            string.Equals(d.Label, label, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
            throw new InvalidOperationException(
                $"No deduction of type '{type}' with label '{label}' found.");

        _deductions.Remove(existing);
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new IncomeSourceDeductionRemoved(Id, type, label));
    }

    // ── Income projection ────────────────────────────────────────────────────

    /// <summary>
    /// The per-paycheck gross amount based on how the income is quoted and
    /// how often paychecks actually arrive.
    /// </summary>
    public decimal PerPaycheckGross() =>
        UserBudgetCalculator.PerPaycheckAmount(
            Amount.Amount, RecurrenceSchedule.Frequency, PaymentFrequency);

    /// <summary>
    /// Number of paychecks landing inside [<paramref name="from"/>,
    /// <paramref name="toExclusive"/>) using the real-world payment anchor.
    /// </summary>
    public int PaychecksInRange(DateTime from, DateTime toExclusive)
    {
        var anchor = LastPaymentDate ?? RecurrenceSchedule.StartDate;
        var schedule = RecurrenceSchedule.Create(PaymentFrequency, anchor, RecurrenceSchedule.EndDate);
        return schedule.GetOccurrencesInRange(from, toExclusive).Count;
    }

    /// <summary>
    /// Projected gross income for a single calendar month.
    /// Returns 0 if the source is inactive or has no occurrences that month.
    /// </summary>
    public decimal ProjectGrossForMonth(int year, int month)
    {
        if (!IsActive) return 0m;
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        if (RecurrenceSchedule.StartDate >= monthEnd) return 0m;
        if (RecurrenceSchedule.EndDate.HasValue && RecurrenceSchedule.EndDate.Value <= monthStart) return 0m;
        return PaychecksInRange(monthStart, monthEnd) * PerPaycheckGross();
    }
}
