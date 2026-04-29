using Finance.Application.Contracts;
using Finance.Application.Managers.Dependencies;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Managers;

internal sealed class HouseholdMembershipManager : IHouseholdMembershipManager
{
    private readonly IHouseholdMembershipRepository _membershipRepository;
    private readonly IHouseholdRepository _householdRepository;

    public HouseholdMembershipManager(
        IHouseholdMembershipRepository membershipRepository,
        IHouseholdRepository householdRepository)
    {
        _membershipRepository = membershipRepository;
        _householdRepository = householdRepository;
    }

    public async Task<MembershipResponse> InviteAsync(InviteHouseholdMemberRequest request, CancellationToken cancellationToken = default)
    {
        var membership = HouseholdMembership.CreateWithInvitation(
            HouseholdId.Create(request.HouseholdId),
            UserId.Create(request.InvitedByUserId),
            request.InvitationCode);

        await _membershipRepository.AddAsync(membership, cancellationToken);
        return Map(membership);
    }

    public async Task<MembershipResponse?> JoinAsync(JoinHouseholdRequest request, CancellationToken cancellationToken = default)
    {
        var membership = await _membershipRepository.GetByInvitationCodeAsync(request.InvitationCode, cancellationToken);
        if (membership is null)
        {
            return null;
        }

        membership.AcceptInvitation(UserId.Create(request.UserId));
        await _membershipRepository.UpdateAsync(membership, cancellationToken);
        return Map(membership);
    }

    public async Task<MembershipResponse?> JoinByCodeAsync(JoinByCodeRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        var membership = await _membershipRepository.GetByInvitationCodeAsync(request.InvitationCode, cancellationToken);
        if (membership is null) return null;

        membership.AcceptInvitation(UserId.Create(userId));
        await _membershipRepository.UpdateAsync(membership, cancellationToken);
        return Map(membership);
    }

    public async Task<MembershipResponse?> LeaveAsync(LeaveHouseholdRequest request, CancellationToken cancellationToken = default)
    {
        var membership = await _membershipRepository.GetByIdAsync(MembershipId.Create(request.MembershipId), cancellationToken);
        if (membership is null)
        {
            return null;
        }

        membership.Remove();
        await _membershipRepository.UpdateAsync(membership, cancellationToken);
        return Map(membership);
    }

    public async Task<MembershipResponse?> ChangeRoleAsync(ChangeMembershipRoleRequest request, CancellationToken cancellationToken = default)
    {
        var membership = await _membershipRepository.GetByIdAsync(MembershipId.Create(request.MembershipId), cancellationToken);
        if (membership is null)
        {
            return null;
        }

        membership.ChangeRole(request.Role);
        await _membershipRepository.UpdateAsync(membership, cancellationToken);
        return Map(membership);
    }

    public async Task<MembershipResponse?> RemoveAsync(RemoveMembershipRequest request, CancellationToken cancellationToken = default)
    {
        var membership = await _membershipRepository.GetByIdAsync(MembershipId.Create(request.MembershipId), cancellationToken);
        if (membership is null)
        {
            return null;
        }

        // TODO: enforce authorization rules based on request.RemovedByUserId.
        membership.Remove();
        await _membershipRepository.UpdateAsync(membership, cancellationToken);
        return Map(membership);
    }

    private static MembershipResponse Map(HouseholdMembership membership)
        => new(
            membership.Id.Value,
            membership.HouseholdId.Value,
            membership.UserId.Value,
            null,
            membership.Role,
            membership.IsActive,
            membership.InvitationCode,
            membership.JoinedAt,
            membership.UpdatedAt);
}
