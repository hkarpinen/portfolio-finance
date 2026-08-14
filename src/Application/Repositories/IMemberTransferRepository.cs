using Finance.Domain.Aggregates;

namespace Finance.Application.Repositories;

public interface IMemberTransferRepository
{
    Task AddAsync(MemberTransfer transfer, CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
}
