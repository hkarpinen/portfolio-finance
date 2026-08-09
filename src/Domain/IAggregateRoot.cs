using Finance.Domain.Events;

namespace Finance.Domain;

// The DbContext drains these events into the outbox inside every SaveChangesAsync, so repositories
// never touch the outbox directly.
public interface IAggregateRoot
{
    IReadOnlyList<DomainEvent> GetDomainEvents();
    void ClearDomainEvents();
}
