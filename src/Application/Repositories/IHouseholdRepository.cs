using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

public interface IHouseholdRepository
{
    Task AddAsync(Household household, CancellationToken cancellationToken = default);
    Task UpdateAsync(Household household, CancellationToken cancellationToken = default);
    Task<Household?> GetByIdAsync(HouseholdId householdId, CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
}
