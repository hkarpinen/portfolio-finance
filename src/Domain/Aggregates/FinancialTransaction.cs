using Finance.Domain.ValueObjects;

namespace Finance.Domain.ReadModels;

// Sign convention: the provider reports outflows as POSITIVE and inflows as NEGATIVE, and that is
// preserved as stored — IsInflow is the direction check, not the sign of Amount alone.
public sealed class FinancialTransaction
{
    public Guid Id { get; private set; }
    public FinancialConnectionId FinancialConnectionId { get; private set; }
    public Guid AccountId { get; private set; }
    public UserId UserId { get; private set; }

    // Globally unique; the idempotency key for upserts across sync calls.
    public string ExternalTransactionId { get; private set; } = string.Empty;

    public Money Amount { get; private set; }
    public DateTime Date { get; private set; }
    public DateTime? AuthorizedDate { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string? MerchantName { get; private set; }
    public string? PrimaryCategory { get; private set; }
    public string? DetailedCategory { get; private set; }
    public bool Pending { get; private set; }
    public bool IsInflow => Amount.Amount < 0m;

    public Guid? LinkedEntityId { get; private set; }
    public string? LinkedEntityType { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private FinancialTransaction() { }

    public static FinancialTransaction Create(
        FinancialConnectionId connectionId,
        Guid accountId,
        UserId userId,
        string externalTransactionId,
        Money amount,
        DateTime date,
        DateTime? authorizedDate,
        string name,
        string? merchantName,
        string? primaryCategory,
        string? detailedCategory,
        bool pending)
    {
        return new FinancialTransaction
        {
            Id = Guid.NewGuid(),
            FinancialConnectionId = connectionId,
            AccountId = accountId,
            UserId = userId,
            ExternalTransactionId = externalTransactionId,
            Amount = amount,
            Date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
            AuthorizedDate = authorizedDate.HasValue
                ? DateTime.SpecifyKind(authorizedDate.Value.Date, DateTimeKind.Utc)
                : null,
            Name = name ?? string.Empty,
            MerchantName = merchantName,
            PrimaryCategory = primaryCategory,
            DetailedCategory = detailedCategory,
            Pending = pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public void ApplyUpdate(
        Money amount,
        DateTime date,
        DateTime? authorizedDate,
        string name,
        string? merchantName,
        string? primaryCategory,
        string? detailedCategory,
        bool pending)
    {
        Amount = amount;
        Date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        AuthorizedDate = authorizedDate.HasValue
            ? DateTime.SpecifyKind(authorizedDate.Value.Date, DateTimeKind.Utc)
            : null;
        Name = name ?? Name;
        MerchantName = merchantName;
        PrimaryCategory = primaryCategory;
        DetailedCategory = detailedCategory;
        Pending = pending;
        UpdatedAt = DateTime.UtcNow;
    }
}
