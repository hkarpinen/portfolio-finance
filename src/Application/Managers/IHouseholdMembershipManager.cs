using Finance.Application.Contracts;

namespace Finance.Application.Managers;

public interface IHouseholdMembershipManager
{
    Task<MembershipResponse> InviteAsync(InviteHouseholdMemberRequest request, CancellationToken cancellationToken = default);
    Task<MembershipResponse?> JoinAsync(JoinHouseholdRequest request, CancellationToken cancellationToken = default);
    /// <summary>Join using only an invitation code — the household is resolved server-side.</summary>
    Task<MembershipResponse?> JoinByCodeAsync(JoinByCodeRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<MembershipResponse?> LeaveAsync(LeaveHouseholdRequest request, CancellationToken cancellationToken = default);
    Task<MembershipResponse?> ChangeRoleAsync(ChangeMembershipRoleRequest request, CancellationToken cancellationToken = default);
    Task<MembershipResponse?> RemoveAsync(RemoveMembershipRequest request, CancellationToken cancellationToken = default);
}
