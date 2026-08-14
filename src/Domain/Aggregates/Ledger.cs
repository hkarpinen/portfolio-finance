using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

// One set of books for one accounting entity. What sort of thing that entity IS does not reach
// in here — the owner is an opaque id with a kind. Accounts and journal entries reference the ledger by id; the root
// itself only fixes whose books these are and the reporting currency. One currency per ledger.
public sealed class Ledger : IAggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public LedgerId Id { get; private set; }

    /// <summary>Whose books these are. The accounting equation only balances within one entity,
    /// which is what makes this the boundary rather than a label on it.</summary>
    public AccountingEntity Owner { get; private set; }
    public string Currency { get; private set; } = "USD";
    public DateTime CreatedAt { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private Ledger() { }

    public static Ledger Open(AccountingEntity owner, string currency)
    {
        if (owner.Id == Guid.Empty)
            throw new ArgumentException("Ledger owner id is required.", nameof(owner));
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(currency));

        var ledger = new Ledger
        {
            Id = LedgerId.New(),
            Owner = owner,
            Currency = currency.ToUpperInvariant(),
            CreatedAt = DateTime.UtcNow,
        };
        ledger._domainEvents.Add(new LedgerOpened(ledger.Id, owner, ledger.Currency));
        return ledger;
    }
}
