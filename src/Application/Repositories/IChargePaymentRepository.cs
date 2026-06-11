using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

public interface IChargePaymentRepository
{
    Task AddAsync(ChargePayment payment, CancellationToken cancellationToken = default);
    Task RemoveAsync(ChargePayment payment, CancellationToken cancellationToken = default);
    Task<ChargePayment?> GetAsync(ChargeId expenseId, DateTime occurrenceDate, CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
}
