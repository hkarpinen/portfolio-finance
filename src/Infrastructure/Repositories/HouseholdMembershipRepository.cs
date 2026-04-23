using Bills.Application.Managers.Dependencies;
using Bills.Domain.Aggregates;
using Bills.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class HouseholdMembershipRepository : IHouseholdMembershipRepository
{
    private readonly BillsDbContext _dbContext;

    public HouseholdMembershipRepository(BillsDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(HouseholdMembership membership, CancellationToken cancellationToken = default)
    {
        await _dbContext.HouseholdMemberships.AddAsync(membership, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(HouseholdMembership membership, CancellationToken cancellationToken = default)
    {
        _dbContext.HouseholdMemberships.Update(membership);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<HouseholdMembership?> GetByIdAsync(MembershipId membershipId, CancellationToken cancellationToken = default)
        => _dbContext.HouseholdMemberships.FirstOrDefaultAsync(m => m.Id == membershipId, cancellationToken);

    public Task<HouseholdMembership?> GetByInvitationCodeAsync(string invitationCode, CancellationToken cancellationToken = default)
        => _dbContext.HouseholdMemberships.FirstOrDefaultAsync(m => m.InvitationCode == invitationCode, cancellationToken);

    public async Task<IReadOnlyCollection<HouseholdMembership>> ListByHouseholdAsync(HouseholdId householdId, CancellationToken cancellationToken = default)
        => await _dbContext.HouseholdMemberships.Where(m => m.HouseholdId == householdId && m.IsActive).ToListAsync(cancellationToken);
}
