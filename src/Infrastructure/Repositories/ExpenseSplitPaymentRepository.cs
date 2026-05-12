using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class ExpenseSplitPaymentRepository : IExpenseSplitPaymentRepository
{
    private readonly FinanceDbContext _dbContext;

    public ExpenseSplitPaymentRepository(FinanceDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(ExpenseSplitPayment payment, CancellationToken cancellationToken = default)
    {
        await _dbContext.ExpenseSplitPayments.AddAsync(payment, cancellationToken);
    }

    public async Task RemoveAsync(ExpenseSplitPayment payment, CancellationToken cancellationToken = default)
    {
        _dbContext.ExpenseSplitPayments.Remove(payment);
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public Task<ExpenseSplitPayment?> GetAsync(ExpenseSplitId splitId, DateTime occurrenceDate, CancellationToken cancellationToken = default)
    {
        var occ = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc);
        return _dbContext.ExpenseSplitPayments
            .FirstOrDefaultAsync(p => p.ExpenseSplitId == splitId && p.OccurrenceDate == occ, cancellationToken);
    }
}
