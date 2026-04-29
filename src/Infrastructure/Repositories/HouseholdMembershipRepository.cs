using Finance.Application.Managers.Dependencies;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class HouseholdMembershipRepository : IHouseholdMembershipRepository
{
    private readonly FinanceDbContext _dbContext;

    public HouseholdMembershipRepository(FinanceDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(HouseholdMembership membership, CancellationToken cancellationToken = default)
    {
        await _dbContext.HouseholdMemberships.AddAsync(membership, cancellationToken);
        foreach (var e in membership.GetDomainEvents()) _dbContext.AddToOutbox(e);
        membership.ClearDomainEvents();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(HouseholdMembership membership, CancellationToken cancellationToken = default)
    {
        _dbContext.HouseholdMemberships.Update(membership);
        foreach (var e in membership.GetDomainEvents()) _dbContext.AddToOutbox(e);
        membership.ClearDomainEvents();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<HouseholdMembership?> GetByIdAsync(MembershipId membershipId, CancellationToken cancellationToken = default)
        => _dbContext.HouseholdMemberships.FirstOrDefaultAsync(m => m.Id == membershipId, cancellationToken);

    public Task<HouseholdMembership?> GetByInvitationCodeAsync(string invitationCode, CancellationToken cancellationToken = default)
        => _dbContext.HouseholdMemberships.FirstOrDefaultAsync(m => m.InvitationCode == invitationCode, cancellationToken);

    public async Task<IReadOnlyCollection<HouseholdMembership>> ListByHouseholdAsync(HouseholdId householdId, CancellationToken cancellationToken = default)
        => await _dbContext.HouseholdMemberships.Where(m => m.HouseholdId == householdId && m.IsActive).ToListAsync(cancellationToken);
}
