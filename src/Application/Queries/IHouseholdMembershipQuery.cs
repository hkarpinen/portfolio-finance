using Finance.Application.Contracts;

namespace Finance.Application.Queries;

public interface IHouseholdMembershipQuery
{
    Task<IReadOnlyCollection<MembershipResponse>> ListMembersAsync(Guid householdId, CancellationToken cancellationToken = default);
}
