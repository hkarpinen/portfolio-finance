using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class ExpensePaymentRepository : IExpensePaymentRepository
{
    private readonly FinanceDbContext _db;

    public ExpensePaymentRepository(FinanceDbContext db) => _db = db;

    public async Task AddAsync(ExpensePayment payment, CancellationToken cancellationToken = default)
    {
        await _db.ExpensePayments.AddAsync(payment, cancellationToken);
    }

    public async Task RemoveAsync(ExpensePayment payment, CancellationToken cancellationToken = default)
    {
        _db.ExpensePayments.Remove(payment);
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);

    public Task<ExpensePayment?> GetAsync(
        ExpenseId expenseId,
        DateTime occurrenceDate,
        CancellationToken cancellationToken = default)
    {
        var date = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc);
        return _db.ExpensePayments
            .FirstOrDefaultAsync(p => p.ExpenseId == expenseId && p.OccurrenceDate == date, cancellationToken);
    }
}
