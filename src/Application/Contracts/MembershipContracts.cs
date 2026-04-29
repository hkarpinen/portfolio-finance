using Finance.Domain.ValueObjects;

namespace Finance.Application.Contracts;

public sealed record InviteHouseholdMemberRequest(
    Guid HouseholdId,
    Guid InvitedByUserId,
    string InvitationCode);

public sealed record JoinHouseholdRequest(
    string InvitationCode,
    Guid UserId);

// Caller-supplies-only-code variant — backend resolves the household from the invitation code
public sealed record JoinByCodeRequest(string InvitationCode);

public sealed record LeaveHouseholdRequest(Guid MembershipId);

public sealed record ChangeMembershipRoleRequest(
    Guid MembershipId,
    HouseholdRole Role);

public sealed record RemoveMembershipRequest(
    Guid MembershipId,
    Guid RemovedByUserId);

public sealed record MembershipResponse(
    Guid MembershipId,
    Guid HouseholdId,
    Guid UserId,
    string? DisplayName,
    HouseholdRole Role,
    bool IsActive,
    string? InvitationCode,
    DateTime JoinedAt,
    DateTime UpdatedAt);

