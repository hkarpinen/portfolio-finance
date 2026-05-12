using Finance.Domain.ValueObjects;

namespace Finance.Application.Commands;

public sealed record InviteHouseholdMemberCommand(
    Guid HouseholdId,
    Guid InvitedByUserId);

public sealed record JoinHouseholdCommand(
    string InvitationCode,
    Guid UserId);

/// <summary>Caller-supplies-only-code variant — backend resolves the household from the invitation code.</summary>
public sealed record JoinByCodeCommand(string InvitationCode);

public sealed record LeaveHouseholdCommand(Guid MembershipId);

public sealed record ChangeMembershipRoleCommand(
    Guid MembershipId,
    HouseholdRole Role);

public sealed record RemoveMembershipCommand(
    Guid MembershipId,
    Guid RemovedByUserId);
