using Finance.Domain.Events;

namespace Finance.Domain;

/// <summary>
/// Marker interface for aggregate roots that carry domain events.
/// <see cref="Infrastructure.Persistence.FinanceDbContext"/> automatically drains
/// these events into the outbox inside every <c>SaveChangesAsync</c> call, so
/// repositories never need to touch the outbox directly.
/// </summary>
public interface IAggregateRoot
{
    IReadOnlyList<DomainEvent> GetDomainEvents();
    void ClearDomainEvents();
}
