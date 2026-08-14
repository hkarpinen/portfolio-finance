using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

public interface IChargeRepository
{
    Task AddAsync(Charge expense, CancellationToken cancellationToken = default);
    Task UpdateAsync(Charge expense, CancellationToken cancellationToken = default);
    Task RemoveAsync(Charge expense, CancellationToken cancellationToken = default);
    Task<Charge?> GetByIdAsync(ChargeId id, CancellationToken cancellationToken = default);
    /// <summary>
    /// This person's own charges that have come due — the set a catch-up posts. Cheap to over-
    /// fetch: converging one already on the books is a no-op, so the filter is on date, not on
    /// whether an entry exists.
    /// </summary>
    Task<IReadOnlyList<Charge>> ListUnpostedPersonalAsync(UserId userId, DateTime asOf, CancellationToken cancellationToken = default);

    Task CommitAsync(CancellationToken cancellationToken = default);
    Task DeleteAllForUserAsync(UserId userId, CancellationToken cancellationToken = default);
}
