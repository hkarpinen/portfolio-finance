using Finance.Application.Commands;
using Finance.Application.Dtos;
using Finance.Application.Ports;
using Finance.Application.Queries;
using Finance.Application.Repositories;
using Finance.Application.Mappers;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Managers;

internal sealed class HouseholdManager : IHouseholdManager
{
    private readonly IHouseholdRepository _householdRepository;
    private readonly IHouseholdMembershipRepository _membershipRepository;
    private readonly IHouseholdQuery _householdQuery;

    public HouseholdManager(
        IHouseholdRepository householdRepository,
        IHouseholdMembershipRepository membershipRepository,
        IHouseholdQuery householdQuery)
    {
        _householdRepository = householdRepository;
        _membershipRepository = membershipRepository;
        _householdQuery = householdQuery;
    }

    // ── Household lifecycle ───────────────────────────────────────────────────

    public async Task<HouseholdDto> CreateAsync(CreateHouseholdCommand request, CancellationToken cancellationToken = default)
    {
        var household = Household.Create(
            request.Name,
            UserId.Create(request.OwnerId),
            request.CurrencyCode,
            request.Description);

        await _householdRepository.AddAsync(household, cancellationToken);

        var ownerMembership = HouseholdMembership.Create(household.Id, UserId.Create(request.OwnerId), HouseholdRole.Owner);
        await _membershipRepository.AddAsync(ownerMembership, cancellationToken);
        await _householdRepository.CommitAsync(cancellationToken);

        return HouseholdMapper.ToResponse(household);
    }

    public async Task<HouseholdDto?> UpdateAsync(UpdateHouseholdCommand request, CancellationToken cancellationToken = default)
    {
        var household = await _householdRepository.GetByIdAsync(HouseholdId.Create(request.HouseholdId), cancellationToken);
        if (household is null) return null;

        household.Update(request.Name, request.Description);
        await _householdRepository.UpdateAsync(household, cancellationToken);
        await _householdRepository.CommitAsync(cancellationToken);
        return HouseholdMapper.ToResponse(household);
    }

    public async Task<HouseholdDto?> TransferOwnershipAsync(TransferHouseholdOwnershipCommand request, CancellationToken cancellationToken = default)
    {
        var household = await _householdRepository.GetByIdAsync(HouseholdId.Create(request.HouseholdId), cancellationToken);
        if (household is null) return null;

        if (household.OwnerId.Value != request.RequestingUserId)
            throw new UnauthorizedAccessException("Only the current owner can transfer household ownership.");

        var newOwnerMembership = await _membershipRepository.GetByUserAndHouseholdAsync(
            UserId.Create(request.NewOwnerId), household.Id, cancellationToken);
        if (newOwnerMembership is null || !newOwnerMembership.IsActive)
            throw new InvalidOperationException("The new owner must be an active member of the household.");

        household.TransferOwnership(UserId.Create(request.NewOwnerId));
        await _householdRepository.UpdateAsync(household, cancellationToken);
        await _householdRepository.CommitAsync(cancellationToken);

        return HouseholdMapper.ToResponse(household);
    }

    public async Task<bool> DeleteAsync(DeleteHouseholdCommand request, CancellationToken cancellationToken = default)
    {
        var household = await _householdRepository.GetByIdAsync(HouseholdId.Create(request.HouseholdId), cancellationToken);
        if (household is null) return false;

        if (household.OwnerId.Value != request.RequestingUserId)
            throw new UnauthorizedAccessException("Only the owner can delete the household.");

        var activeMembers = await _householdQuery.ListMembersAsync(household.Id.Value, cancellationToken);
        var activeMemberCount = activeMembers.Count(m => m.IsActive);
        household.Deactivate(activeMemberCount);
        await _householdRepository.UpdateAsync(household, cancellationToken);
        await _householdRepository.CommitAsync(cancellationToken);
        return true;
    }

    // ── Membership ────────────────────────────────────────────────────────────

    public async Task<MembershipDto> InviteAsync(InviteHouseholdMemberCommand request, CancellationToken cancellationToken = default)
    {
        var code = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var membership = HouseholdMembership.CreateWithInvitation(
            HouseholdId.Create(request.HouseholdId),
            UserId.Create(request.InvitedByUserId),
            code);

        await _membershipRepository.AddAsync(membership, cancellationToken);
        await _membershipRepository.CommitAsync(cancellationToken);
        return MembershipMapper.ToResponse(membership);
    }

    public async Task<MembershipDto?> JoinAsync(JoinHouseholdCommand request, CancellationToken cancellationToken = default)
    {
        var membership = await _membershipRepository.GetByInvitationCodeAsync(request.InvitationCode, cancellationToken);
        if (membership is null) return null;

        membership.AcceptInvitation(UserId.Create(request.UserId));
        await _membershipRepository.UpdateAsync(membership, cancellationToken);
        await _membershipRepository.CommitAsync(cancellationToken);
        return MembershipMapper.ToResponse(membership);
    }

    public async Task<MembershipDto?> JoinByCodeAsync(JoinByCodeCommand request, Guid userId, CancellationToken cancellationToken = default)
    {
        var membership = await _membershipRepository.GetByInvitationCodeAsync(request.InvitationCode, cancellationToken);
        if (membership is null) return null;

        membership.AcceptInvitation(UserId.Create(userId));
        await _membershipRepository.UpdateAsync(membership, cancellationToken);
        await _membershipRepository.CommitAsync(cancellationToken);
        return MembershipMapper.ToResponse(membership);
    }

    public async Task<MembershipDto?> LeaveAsync(LeaveHouseholdCommand request, CancellationToken cancellationToken = default)
    {
        var membership = await _membershipRepository.GetByIdAsync(MembershipId.Create(request.MembershipId), cancellationToken);
        if (membership is null) return null;

        membership.Remove();
        await _membershipRepository.UpdateAsync(membership, cancellationToken);
        await _membershipRepository.CommitAsync(cancellationToken);
        return MembershipMapper.ToResponse(membership);
    }

    public async Task<MembershipDto?> ChangeRoleAsync(ChangeMembershipRoleCommand request, CancellationToken cancellationToken = default)
    {
        var membership = await _membershipRepository.GetByIdAsync(MembershipId.Create(request.MembershipId), cancellationToken);
        if (membership is null) return null;

        membership.ChangeRole(request.Role);
        await _membershipRepository.UpdateAsync(membership, cancellationToken);
        await _membershipRepository.CommitAsync(cancellationToken);
        return MembershipMapper.ToResponse(membership);
    }

    public async Task<MembershipDto?> RemoveAsync(RemoveMembershipCommand request, CancellationToken cancellationToken = default)
    {
        var membership = await _membershipRepository.GetByIdAsync(MembershipId.Create(request.MembershipId), cancellationToken);
        if (membership is null) return null;

        var household = await _householdRepository.GetByIdAsync(membership.HouseholdId, cancellationToken);
        if (household is null) return null;

        if (request.RemovedByUserId != household.OwnerId.Value &&
            request.RemovedByUserId != membership.UserId.Value)
            throw new UnauthorizedAccessException(
                "Only the household owner or the member themselves may remove a membership.");

        membership.Remove();
        await _membershipRepository.UpdateAsync(membership, cancellationToken);
        await _membershipRepository.CommitAsync(cancellationToken);
        return MembershipMapper.ToResponse(membership);
    }
}
