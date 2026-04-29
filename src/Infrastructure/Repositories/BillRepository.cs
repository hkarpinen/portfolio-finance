using Finance.Application.Managers.Dependencies;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class BillRepository : IBillRepository
{
    private readonly FinanceDbContext _dbContext;

    public BillRepository(FinanceDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(Bill bill, CancellationToken cancellationToken = default)
    {
        await _dbContext.Bills.AddAsync(bill, cancellationToken);
        foreach (var e in bill.GetDomainEvents()) _dbContext.AddToOutbox(e);
        bill.ClearDomainEvents();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Bill bill, CancellationToken cancellationToken = default)
    {
        _dbContext.Bills.Update(bill);
        foreach (var e in bill.GetDomainEvents()) _dbContext.AddToOutbox(e);
        bill.ClearDomainEvents();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<Bill?> GetByIdAsync(BillId billId, CancellationToken cancellationToken = default)
        => _dbContext.Bills.FirstOrDefaultAsync(b => b.Id == billId, cancellationToken);
}
