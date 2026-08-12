using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

public interface IAllocationRepository
{
    Task AddAsync(Allocation split, CancellationToken cancellationToken = default);
    Task UpdateAsync(Allocation split, CancellationToken cancellationToken = default);
    Task RemoveAsync(Allocation split, CancellationToken cancellationToken = default);
    Task<Allocation?> GetByIdAsync(AllocationId splitId, CancellationToken cancellationToken = default);
    Task<Allocation?> GetByChargeAndUserAsync(ChargeId expenseId, UserId userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Allocation>> ListByChargeAsync(ChargeId chargeId, CancellationToken cancellationToken = default);

    /// <summary>What a charge is already split by, optionally ignoring one allocation — the shape
    /// an upsert needs, so it can ask "does mine still fit alongside the others".</summary>
    Task<decimal> SumForChargeAsync(ChargeId chargeId, AllocationId? excluding = null, CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task DeleteAllForUserAsync(UserId userId, CancellationToken cancellationToken = default);
}
