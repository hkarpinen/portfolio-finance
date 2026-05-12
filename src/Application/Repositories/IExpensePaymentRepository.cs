using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

public interface IExpensePaymentRepository
{
    Task AddAsync(ExpensePayment payment, CancellationToken cancellationToken = default);
    Task RemoveAsync(ExpensePayment payment, CancellationToken cancellationToken = default);
    Task<ExpensePayment?> GetAsync(ExpenseId expenseId, DateTime occurrenceDate, CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
}
