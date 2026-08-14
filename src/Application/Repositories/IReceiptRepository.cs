using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

public interface IReceiptRepository
{
    Task<Receipt?> GetByIdAsync(ReceiptId id, CancellationToken ct = default);
    Task AddAsync(Receipt receipt, CancellationToken ct = default);
    Task UpdateAsync(Receipt receipt, CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
}
