using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// Application-layer data model for a recurring cash-flow pattern detected by the financial
/// data provider. Used as the source of truth for auto-creating <c>Expense</c> (outflow)
/// and <c>IncomeSource</c> (inflow) candidates.
/// Not a domain aggregate — persisted via EF through <see cref="Finance.Application.Repositories.IFinancialConnectionRepository"/>.
/// </summary>
public sealed class RecurringSuggestion
{
    public Guid Id { get; private set; }
    public FinancialConnectionId FinancialConnectionId { get; private set; }
    public Guid AccountId { get; private set; }
    public UserId UserId { get; private set; }

    /// <summary>Provider-issued stable stream identifier; the idempotency key for upserts.</summary>
    public string ExternalStreamId { get; private set; } = string.Empty;

    public RecurringFlowDirection Direction { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? MerchantName { get; private set; }
    public RecurrenceFrequency Frequency { get; private set; }

    public Money AverageAmount { get; private set; }
    public Money LastAmount { get; private set; }

    public DateTime FirstDate { get; private set; }
    public DateTime LastDate { get; private set; }
    public DateTime? PredictedNextDate { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>True once auto-linked and a domain entity has been created from this suggestion.</summary>
    public bool IsLinked { get; private set; }

    /// <summary>FK back to the created <c>Expense</c> or <c>IncomeSource</c>, if linked.</summary>
    public Guid? LinkedEntityId { get; private set; }
    public string? LinkedEntityType { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private RecurringSuggestion() { }

    public static RecurringSuggestion Create(
        FinancialConnectionId connectionId,
        Guid accountId,
        UserId userId,
        string externalStreamId,
        RecurringFlowDirection direction,
        string description,
        string? merchantName,
        RecurrenceFrequency frequency,
        Money averageAmount,
        Money lastAmount,
        DateTime firstDate,
        DateTime lastDate,
        DateTime? predictedNextDate,
        bool isActive)
    {
        return new RecurringSuggestion
        {
            Id = Guid.NewGuid(),
            FinancialConnectionId = connectionId,
            AccountId = accountId,
            UserId = userId,
            ExternalStreamId = externalStreamId,
            Direction = direction,
            Description = description ?? string.Empty,
            MerchantName = merchantName,
            Frequency = frequency,
            AverageAmount = averageAmount,
            LastAmount = lastAmount,
            FirstDate = DateTime.SpecifyKind(firstDate.Date, DateTimeKind.Utc),
            LastDate = DateTime.SpecifyKind(lastDate.Date, DateTimeKind.Utc),
            PredictedNextDate = predictedNextDate.HasValue
                ? DateTime.SpecifyKind(predictedNextDate.Value.Date, DateTimeKind.Utc)
                : null,
            IsActive = isActive,
            IsLinked = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public void ApplyUpdate(
        string description,
        string? merchantName,
        RecurrenceFrequency frequency,
        Money averageAmount,
        Money lastAmount,
        DateTime firstDate,
        DateTime lastDate,
        DateTime? predictedNextDate,
        bool isActive)
    {
        Description = description ?? Description;
        MerchantName = merchantName;
        Frequency = frequency;
        AverageAmount = averageAmount;
        LastAmount = lastAmount;
        FirstDate = DateTime.SpecifyKind(firstDate.Date, DateTimeKind.Utc);
        LastDate = DateTime.SpecifyKind(lastDate.Date, DateTimeKind.Utc);
        PredictedNextDate = predictedNextDate.HasValue
            ? DateTime.SpecifyKind(predictedNextDate.Value.Date, DateTimeKind.Utc)
            : null;
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkLinked(Guid entityId, string entityType)
    {
        LinkedEntityId = entityId;
        LinkedEntityType = entityType;
        IsLinked = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Unlink()
    {
        IsLinked = false;
        LinkedEntityId = null;
        LinkedEntityType = null;
        UpdatedAt = DateTime.UtcNow;
    }
}
