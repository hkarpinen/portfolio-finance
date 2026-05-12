using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// Represents a user's link to a financial institution via Plaid ("Item" in Plaid terms).
/// This is the only Plaid-sourced concept that rises to a true domain aggregate: it holds
/// an encrypted long-lived access token, a sync cursor, and emits domain events when the
/// connection's status changes (established, requires reauth, revoked).
///
/// Performance contract: callers MUST pass <see cref="Cursor"/> to Plaid's sync endpoint
/// and overwrite it with the value returned by Plaid only after persisting the resulting
/// changes. This guarantees we never re-fetch data already known to the system, and
/// recovers cleanly from partial failures.
/// </summary>
public class FinancialConnection : IAggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public FinancialConnectionId Id { get; private set; }
    public UserId UserId { get; private set; }

    /// <summary>
    /// Opaque identifier issued by the financial data provider for this connection;
    /// stable for the lifetime of the link and used as the idempotency key on re-link.
    /// </summary>
    public string ExternalId { get; private set; } = string.Empty;

    /// <summary>The institution display name at link time (e.g. "Chase").</summary>
    public string InstitutionName { get; private set; } = string.Empty;

    /// <summary>Plaid-issued institution identifier; null in sandbox for some flows.</summary>
    public string? InstitutionId { get; private set; }

    /// <summary>
    /// The <c>access_token</c> issued by <c>/item/public_token/exchange</c>, encrypted with
    /// ASP.NET Data Protection. Plaintext access tokens MUST NEVER be persisted.
    /// </summary>
    public string EncryptedAccessToken { get; private set; } = string.Empty;

    /// <summary>
    /// Plaid <c>/transactions/sync</c> cursor. Null until the first successful sync;
    /// from then on always advances forward.
    /// </summary>
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

    /// <summary>Advances the sync cursor after a successful transaction sync round-trip.</summary>
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
    }

    public void MarkRevoked()
    {
        Status = FinancialConnectionStatus.Revoked;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new FinancialConnectionRevoked(Id, UserId));
    }
}
