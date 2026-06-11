using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// A self-balancing book of accounts, scoped to an owner — a <c>Group</c> (opaque
/// GroupId) or a <c>User</c> (UserId). Finance knows nothing of "household": the
/// owner is just a typed opaque id. Accounts and journal entries reference this
/// ledger by id; the ledger root itself only fixes ownership and the reporting
/// currency (P10 — one currency per ledger).
/// </summary>
public sealed class Ledger : IAggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public LedgerId Id { get; private set; }
    public LedgerOwnerType OwnerType { get; private set; }
    public Guid OwnerId { get; private set; }
    public string Currency { get; private set; } = "USD";
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private Ledger() { }

    public static Ledger Open(LedgerOwnerType ownerType, Guid ownerId, string currency)
    {
        if (ownerId == Guid.Empty)
            throw new ArgumentException("Ledger owner id is required.", nameof(ownerId));
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(currency));

        var ledger = new Ledger
        {
            Id = LedgerId.New(),
            OwnerType = ownerType,
            OwnerId = ownerId,
            Currency = currency.ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow,
        };
        ledger._domainEvents.Add(new LedgerOpened(ledger.Id, ownerType, ownerId, ledger.Currency));
        return ledger;
    }
}
