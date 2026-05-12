using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// Application-layer data model for a single transaction synced from a linked financial institution.
/// Identified by the immutable <see cref="ExternalTransactionId"/> which is the
/// idempotency key for upserts across multiple sync calls.
///
/// Sign convention: the provider reports outflows as positive and inflows as negative.
/// We preserve that via <see cref="IsInflow"/> for direction checks.
/// </summary>
public sealed class FinancialTransaction
{
    public Guid Id { get; private set; }
    public FinancialConnectionId FinancialConnectionId { get; private set; }
    public Guid AccountId { get; private set; }
    public UserId UserId { get; private set; }

    /// <summary>Provider-issued stable transaction identifier. Globally unique; used as the idempotency key.</summary>
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
