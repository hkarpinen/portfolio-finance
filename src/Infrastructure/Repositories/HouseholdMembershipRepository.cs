using Finance.Application.Repositories;
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
    }

    public async Task UpdateAsync(HouseholdMembership membership, CancellationToken cancellationToken = default)
    {
        _dbContext.HouseholdMemberships.Update(membership);
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public Task<HouseholdMembership?> GetByIdAsync(MembershipId membershipId, CancellationToken cancellationToken = default)
        => _dbContext.HouseholdMemberships.FirstOrDefaultAsync(m => m.Id == membershipId, cancellationToken);

    public Task<HouseholdMembership?> GetByUserAndHouseholdAsync(UserId userId, HouseholdId householdId, CancellationToken cancellationToken = default)
        => _dbContext.HouseholdMemberships.FirstOrDefaultAsync(m => m.UserId == userId && m.HouseholdId == householdId && m.IsActive, cancellationToken);

    public Task<HouseholdMembership?> GetByInvitationCodeAsync(string invitationCode, CancellationToken cancellationToken = default)
        => _dbContext.HouseholdMemberships.FirstOrDefaultAsync(m => m.InvitationCode == invitationCode, cancellationToken);

    public async Task<IReadOnlyCollection<HouseholdMembership>> GetByIdsAsync(IReadOnlyCollection<MembershipId> membershipIds, CancellationToken cancellationToken = default)
        => await _dbContext.HouseholdMemberships.Where(m => membershipIds.Contains(m.Id)).ToListAsync(cancellationToken);
}
