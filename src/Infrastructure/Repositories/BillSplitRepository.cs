using Finance.Application.Contracts;
using Finance.Application.Managers.Dependencies;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class BillSplitRepository : IBillSplitRepository
{
    private readonly FinanceDbContext _dbContext;

    public BillSplitRepository(FinanceDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(BillSplit split, CancellationToken cancellationToken = default)
    {
        await _dbContext.BillSplits.AddAsync(split, cancellationToken);
        foreach (var e in split.GetDomainEvents()) _dbContext.AddToOutbox(e);
        split.ClearDomainEvents();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(BillSplit split, CancellationToken cancellationToken = default)
    {
        _dbContext.BillSplits.Update(split);
        foreach (var e in split.GetDomainEvents()) _dbContext.AddToOutbox(e);
        split.ClearDomainEvents();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(BillSplit split, CancellationToken cancellationToken = default)
    {
        _dbContext.BillSplits.Remove(split);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<BillSplit?> GetByIdAsync(SplitId splitId, CancellationToken cancellationToken = default)
        => _dbContext.BillSplits.FirstOrDefaultAsync(s => s.Id == splitId, cancellationToken);

    public Task<BillSplit?> GetByBillAndMembershipAsync(BillId billId, MembershipId membershipId, CancellationToken cancellationToken = default)
        => _dbContext.BillSplits.FirstOrDefaultAsync(s => s.BillId == billId && s.MembershipId == membershipId, cancellationToken);
}
