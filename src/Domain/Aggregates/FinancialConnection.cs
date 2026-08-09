using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

// Sync contract: callers MUST pass Cursor to the provider's sync endpoint and overwrite it with
// the value the provider returns only AFTER persisting the resulting changes. That is what keeps
// a partial failure from either re-fetching known data or skipping unseen data.
public class FinancialConnection : IAggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public FinancialConnectionId Id { get; private set; }
    public UserId UserId { get; private set; }

    // Stable for the lifetime of the link; used as the idempotency key on re-link.
    public string ExternalId { get; private set; } = string.Empty;

    public string InstitutionName { get; private set; } = string.Empty;

    // Null in sandbox for some flows.
    public string? InstitutionId { get; private set; }

    // Encrypted with ASP.NET Data Protection. Plaintext access tokens MUST NEVER be persisted.
    public string EncryptedAccessToken { get; private set; } = string.Empty;

    // Null until the first successful sync; from then on only ever advances forward.
    public string? Cursor { get; private set; }

    public FinancialConnectionStatus Status { get; private set; }

    public DateTime? LastSyncedAt { get; private set; }
    public DateTime? LastWebhookAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private FinancialConnection() { }

    public static FinancialConnection Connect(
        UserId userId,
        string externalId,
        string institutionName,
        string? institutionId,
        string encryptedAccessToken)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("Provider connection id is required.", nameof(externalId));
        if (string.IsNullOrWhiteSpace(encryptedAccessToken))
            throw new ArgumentException("Access token is required.", nameof(encryptedAccessToken));

        var connection = new FinancialConnection
        {
            Id = FinancialConnectionId.New(),
            UserId = userId,
            ExternalId = externalId,
            InstitutionName = string.IsNullOrWhiteSpace(institutionName) ? "Unknown" : institutionName,
            InstitutionId = institutionId,
            EncryptedAccessToken = encryptedAccessToken,
            Cursor = null,
            Status = FinancialConnectionStatus.Healthy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        connection._domainEvents.Add(new FinancialConnectionEstablished(connection.Id, userId, connection.InstitutionName));
        return connection;
    }

    public void AdvanceCursor(string newCursor)
    {
        Cursor = newCursor ?? string.Empty;
        LastSyncedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordWebhook()
    {
        LastWebhookAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkRequiresReauth()
    {
        if (Status == FinancialConnectionStatus.RequiresReauth) return;
        Status = FinancialConnectionStatus.RequiresReauth;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new FinancialConnectionRequiresReauth(Id, UserId));
    }

    public void MarkHealthy()
    {
        if (Status == FinancialConnectionStatus.Healthy) return;
        Status = FinancialConnectionStatus.Healthy;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new FinancialConnectionHealthy(Id, UserId));
    }

    public void MarkRevoked()
    {
        Status = FinancialConnectionStatus.Revoked;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new FinancialConnectionRevoked(Id, UserId));
    }
}
