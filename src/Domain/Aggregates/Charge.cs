using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>GroupId discriminates: null is personal, set is shared.</summary>
public class Charge : IAggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public ChargeId Id { get; private set; }
    public UserId UserId { get; private set; }

    public GroupId? GroupId { get; private set; }

    public UserId? CreatedBy { get; private set; }

    /// <summary>Who FRONTED the bill — not necessarily who created it.</summary>
    public Guid? PayerUserId { get; private set; }

    /// <summary>
    /// PayerMember: the payer fronts it and debtors repay them. GroupCash: paid from the
    /// pool, and EVERY member settles — the payer included. Meaningless when personal.
    /// </summary>
    public FundingSource FundingSource { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Money Amount { get; private set; }
    public ChargeCategory Category { get; private set; }
    public DateTime DueDate { get; private set; }
    public RecurrenceSchedule? RecurrenceSchedule { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private Charge()
    {
    }

    /// <summary>Creates a personal expense (GroupId = null).</summary>
    public static Charge Create(
        UserId userId,
        string title,
        Money amount,
        ChargeCategory category,
        DateTime dueDate,
        RecurrenceSchedule? recurrenceSchedule = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        var expense = new Charge
        {
            Id = ChargeId.New(),
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

        expense._domainEvents.Add(new ChargeCreated(
            expense.Id,
            userId,
            title,
            amount,
            category,
            dueDate,
            recurrenceSchedule));

        return expense;
    }

    /// <summary>UserId is set to the CREATOR here, unlike a personal charge's owner.</summary>
    public static Charge CreateGroup(
        GroupId groupId,
        UserId createdBy,
        string title,
        Money amount,
        ChargeCategory category,
        DateTime dueDate,
        RecurrenceSchedule? recurrenceSchedule = null,
        string? description = null,
        Guid? payerUserId = null,
        FundingSource fundingSource = FundingSource.PayerMember)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty.", nameof(title));
        if (amount.Amount < 0)
            throw new ArgumentException("Amount cannot be negative.", nameof(amount));

        var expense = new Charge
        {
            Id = ChargeId.New(),
            UserId = createdBy,
            GroupId = groupId,
            CreatedBy = createdBy,
            PayerUserId = payerUserId,
            FundingSource = fundingSource,
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

        expense._domainEvents.Add(new ChargeCreated(
            expense.Id,
            createdBy,
            title,
            amount,
            category,
            dueDate,
            recurrenceSchedule,
            groupId,
            payerUserId));

        return expense;
    }

    public void Update(
        string title,
        Money amount,
        ChargeCategory category,
        DateTime dueDate,
        RecurrenceSchedule? recurrenceSchedule = null,
        string? description = null,
        Guid? payerUserId = null)
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
        // Only mutate payer when explicitly supplied; null on personal expenses, optional on group
        if (GroupId is not null && payerUserId is not null)
            PayerUserId = payerUserId;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new ChargeUpdated(Id, title, amount, category, dueDate, recurrenceSchedule, GroupId, PayerUserId));
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new InvalidOperationException("Charge is already inactive.");

        IsActive = false;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new ChargeDeactivated(Id, GroupId));
    }

    public void Activate()
    {
        if (IsActive)
            throw new InvalidOperationException("Charge is already active.");

        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new ChargeActivated(Id, GroupId));
    }

    public bool TryDeactivate()
    {
        if (!IsActive) return false;
        Deactivate();
        return true;
    }

    /// <summary>
    /// Record that the vendor was paid for this group charge — fronted from a member's pocket
    /// (<see cref="FundingSource.PayerMember"/>, with <paramref name="paidByUserId"/> the payer) or
    /// from the shared pot (<see cref="FundingSource.GroupCash"/>). Sets the funding of record (which
    /// member settlement reads) and raises the fact. The ledger posting (Dr Vendor Payable / Cr the
    /// funding account) is recorded separately by the application layer; "is it paid" is derived from
    /// the ledger (Vendor Payable balance), not stored here.
    /// </summary>
    public void RecordVendorPayment(DateTime occurrenceDate, FundingSource fundingSource, Guid? paidByUserId)
    {
        if (GroupId is null)
            throw new InvalidOperationException("Vendor payment applies to group charges only.");

        FundingSource = fundingSource;
        if (fundingSource == FundingSource.PayerMember && paidByUserId is not null)
            PayerUserId = paidByUserId;
        UpdatedAt = DateTime.UtcNow;

        var occurrence = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc);
        _domainEvents.Add(new VendorPaid(Id, occurrence, fundingSource, paidByUserId, DateTime.UtcNow));
    }

    /// <summary>Undo the vendor payment for an occurrence (the ledger reversal is posted by the
    /// application layer). The funding of record is left in place as the intended default.</summary>
    public void ReverseVendorPayment(DateTime occurrenceDate)
    {
        if (GroupId is null)
            throw new InvalidOperationException("Vendor payment applies to group charges only.");

        UpdatedAt = DateTime.UtcNow;
        var occurrence = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc);
        _domainEvents.Add(new VendorPaymentReversed(Id, occurrence));
    }
}
