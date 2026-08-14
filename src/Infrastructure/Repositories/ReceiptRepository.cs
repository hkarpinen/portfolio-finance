using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class ReceiptRepository : IReceiptRepository
{
    private readonly FinanceDbContext _db;

    public ReceiptRepository(FinanceDbContext db) => _db = db;

    public Task<Receipt?> GetByIdAsync(ReceiptId id, CancellationToken ct = default)
        => _db.Receipts.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task AddAsync(Receipt receipt, CancellationToken ct = default)
        => await _db.Receipts.AddAsync(receipt, ct);

    public Task UpdateAsync(Receipt receipt, CancellationToken ct = default)
    {
        _db.Receipts.Update(receipt);
        return Task.CompletedTask;
    }

    public Task CommitAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
