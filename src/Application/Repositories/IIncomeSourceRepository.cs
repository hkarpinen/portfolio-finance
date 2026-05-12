using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

public interface IIncomeSourceRepository
{
    Task AddAsync(IncomeSource incomeSource, CancellationToken cancellationToken = default);
    Task UpdateAsync(IncomeSource incomeSource, CancellationToken cancellationToken = default);
    Task RemoveAsync(IncomeSource incomeSource, CancellationToken cancellationToken = default);
    Task<IncomeSource?> GetByIdAsync(IncomeId incomeId, CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
}
