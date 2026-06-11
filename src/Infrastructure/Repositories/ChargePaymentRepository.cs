using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class ChargePaymentRepository : IChargePaymentRepository
{
    private readonly FinanceDbContext _db;

    public ChargePaymentRepository(FinanceDbContext db) => _db = db;

    public async Task AddAsync(ChargePayment payment, CancellationToken cancellationToken = default)
    {
        await _db.ChargePayments.AddAsync(payment, cancellationToken);
    }

    public async Task RemoveAsync(ChargePayment payment, CancellationToken cancellationToken = default)
    {
        _db.ChargePayments.Remove(payment);
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);

    public Task<ChargePayment?> GetAsync(
        ChargeId expenseId,
        DateTime occurrenceDate,
        CancellationToken cancellationToken = default)
    {
        var date = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc);
        return _db.ChargePayments
            .FirstOrDefaultAsync(p => p.ChargeId == expenseId && p.OccurrenceDate == date, cancellationToken);
    }
}
