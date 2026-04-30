using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// Income source aggregate root representing a member's income contribution to the household.
/// </summary>
public class IncomeSource
{
    private readonly List<DomainEvent> _domainEvents = new();

    public IncomeId Id { get; private set; }
    public UserId UserId { get; private set; }
    public Money Amount { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public RecurrenceSchedule RecurrenceSchedule { get; private set; } = null!;
    /// <summary>Optional: the date the user last received this income. Used for per-month projection calculations.</summary>
    public DateTime? LastPaymentDate { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsActive { get; private set; }

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
    public List<PayrollDeduction> Deductions { get; private set; } = new();

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
        DateTime? lastPaymentDate = null)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Source cannot be empty.", nameof(source));

        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        var incomeSource = new IncomeSource
        {
            Id = IncomeId.New(),
            UserId = userId,
            Amount = amount,
            Source = source,
            RecurrenceSchedule = recurrenceSchedule,
            LastPaymentDate = lastPaymentDate.HasValue ? DateTime.SpecifyKind(lastPaymentDate.Value, DateTimeKind.Utc) : null,
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

        var existing = Deductions.FirstOrDefault(d =>
            d.Type == deduction.Type &&
            string.Equals(d.Label, deduction.Label, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
            throw new InvalidOperationException(
                $"A deduction of type '{deduction.Type}' with label '{deduction.Label}' already exists.");

        Deductions.Add(deduction);
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new IncomeSourceDeductionAdded(Id, deduction));
    }

    public void RemoveDeduction(DeductionType type, string label)
    {
        var existing = Deductions.FirstOrDefault(d =>
            d.Type == type &&
            string.Equals(d.Label, label, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
            throw new InvalidOperationException(
                $"No deduction of type '{type}' with label '{label}' found.");

        Deductions.Remove(existing);
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new IncomeSourceDeductionRemoved(Id, type, label));
    }

    public void UpdateDeduction(DeductionType type, string label, PayrollDeduction replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        var idx = Deductions.FindIndex(d =>
            d.Type == type &&
            string.Equals(d.Label, label, StringComparison.OrdinalIgnoreCase));

        if (idx < 0)
            throw new InvalidOperationException(
                $"No deduction of type '{type}' with label '{label}' found.");

        Deductions[idx] = replacement;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new IncomeSourceDeductionUpdated(Id, replacement));
    }
}
