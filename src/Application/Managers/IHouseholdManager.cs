using Finance.Application.Commands;
using Finance.Application.Dtos;

namespace Finance.Application.Managers;

public interface IHouseholdManager
{
    // ── Household lifecycle ───────────────────────────────────────────────────
    Task<HouseholdDto> CreateAsync(CreateHouseholdCommand request, CancellationToken cancellationToken = default);
    Task<HouseholdDto?> UpdateAsync(UpdateHouseholdCommand request, CancellationToken cancellationToken = default);
    Task<HouseholdDto?> TransferOwnershipAsync(TransferHouseholdOwnershipCommand request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(DeleteHouseholdCommand request, CancellationToken cancellationToken = default);

    // ── Membership ────────────────────────────────────────────────────────────
    Task<MembershipDto> InviteAsync(InviteHouseholdMemberCommand request, CancellationToken cancellationToken = default);
    Task<MembershipDto?> JoinAsync(JoinHouseholdCommand request, CancellationToken cancellationToken = default);
    Task<MembershipDto?> JoinByCodeAsync(JoinByCodeCommand request, Guid userId, CancellationToken cancellationToken = default);
    Task<MembershipDto?> LeaveAsync(LeaveHouseholdCommand request, CancellationToken cancellationToken = default);
    Task<MembershipDto?> ChangeRoleAsync(ChangeMembershipRoleCommand request, CancellationToken cancellationToken = default);
    Task<MembershipDto?> RemoveAsync(RemoveMembershipCommand request, CancellationToken cancellationToken = default);
}
