using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Infrastructure.Persistence;

namespace Infrastructure.Repositories;

internal sealed class MemberTransferRepository : IMemberTransferRepository
{
    private readonly FinanceDbContext _db;

    public MemberTransferRepository(FinanceDbContext db) => _db = db;

    public async Task AddAsync(MemberTransfer transfer, CancellationToken ct = default)
        => await _db.MemberTransfers.AddAsync(transfer, ct);

    public Task CommitAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
