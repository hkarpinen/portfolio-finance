using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

public interface IChargeRepository
{
    Task AddAsync(Charge expense, CancellationToken cancellationToken = default);
    Task UpdateAsync(Charge expense, CancellationToken cancellationToken = default);
    Task RemoveAsync(Charge expense, CancellationToken cancellationToken = default);
    Task<Charge?> GetByIdAsync(ChargeId id, CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task DeleteAllForUserAsync(UserId userId, CancellationToken cancellationToken = default);
}
