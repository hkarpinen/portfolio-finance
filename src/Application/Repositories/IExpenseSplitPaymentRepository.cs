using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

public interface IExpenseSplitPaymentRepository
{
    Task AddAsync(ExpenseSplitPayment payment, CancellationToken cancellationToken = default);
    Task RemoveAsync(ExpenseSplitPayment payment, CancellationToken cancellationToken = default);
    Task<ExpenseSplitPayment?> GetAsync(ExpenseSplitId splitId, DateTime occurrenceDate, CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
}
