using Bills.Application.Contracts;

namespace Bills.Application.Queries;

public interface IHouseholdMembershipQuery
{
    Task<IReadOnlyCollection<MembershipResponse>> ListMembersAsync(Guid householdId, CancellationToken cancellationToken = default);
}
