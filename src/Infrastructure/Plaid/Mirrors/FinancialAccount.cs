using Finance.Domain.ValueObjects;

namespace Infrastructure.Plaid.Mirrors;

public sealed class FinancialAccount
{
    public Guid Id { get; private set; }
    public FinancialConnectionId FinancialConnectionId { get; private set; }
    public UserId UserId { get; private set; }

    // Unique per connection; the upsert key.
    public string ExternalAccountId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;
    public string? OfficialName { get; private set; }

    // Last 2-4 digits of the account number — null when the institution does not surface it.
    public string? Mask { get; private set; }

    public string Type { get; private set; } = string.Empty;
    public string? Subtype { get; private set; }
    public string CurrencyCode { get; private set; } = "USD";
    public decimal? CurrentBalance { get; private set; }
    public decimal? AvailableBalance { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private FinancialAccount() { }

    public static FinancialAccount Create(
        FinancialConnectionId connectionId,
        UserId userId,
        string externalAccountId,
        string name,
        string? officialName,
        string? mask,
        string type,
        string? subtype,
        string currencyCode,
        decimal? currentBalance,
        decimal? availableBalance)
    {
        if (string.IsNullOrWhiteSpace(externalAccountId))
            throw new ArgumentException("Provider account id is required.", nameof(externalAccountId));

        return new FinancialAccount
        {
            Id = Guid.NewGuid(),
            FinancialConnectionId = connectionId,
            UserId = userId,
            ExternalAccountId = externalAccountId,
            Name = string.IsNullOrWhiteSpace(name) ? "Account" : name,
            OfficialName = officialName,
            Mask = mask,
            Type = type ?? "other",
            Subtype = subtype,
            CurrencyCode = string.IsNullOrWhiteSpace(currencyCode) ? "USD" : currencyCode.ToUpperInvariant(),
            CurrentBalance = currentBalance,
            AvailableBalance = availableBalance,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public void UpdateMetadata(
        string name,
        string? officialName,
        string? mask,
        string type,
        string? subtype,
        decimal? currentBalance,
        decimal? availableBalance)
    {
        Name = string.IsNullOrWhiteSpace(name) ? Name : name;
        OfficialName = officialName;
        Mask = mask;
        Type = type ?? Type;
        Subtype = subtype;
        CurrentBalance = currentBalance;
        AvailableBalance = availableBalance;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (!IsActive) return;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
