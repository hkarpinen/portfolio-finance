using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

public interface IHouseholdMembershipRepository
{
    Task AddAsync(HouseholdMembership membership, CancellationToken cancellationToken = default);
    Task UpdateAsync(HouseholdMembership membership, CancellationToken cancellationToken = default);
    Task<HouseholdMembership?> GetByIdAsync(MembershipId membershipId, CancellationToken cancellationToken = default);
    Task<HouseholdMembership?> GetByUserAndHouseholdAsync(UserId userId, HouseholdId householdId, CancellationToken cancellationToken = default);
    Task<HouseholdMembership?> GetByInvitationCodeAsync(string invitationCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<HouseholdMembership>> GetByIdsAsync(IReadOnlyCollection<MembershipId> membershipIds, CancellationToken cancellationToken = default);
    Task CommitAsync(CancellationToken cancellationToken = default);
}
