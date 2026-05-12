using Finance.Domain.ValueObjects;

namespace Finance.Application.Dtos;

public sealed record MembershipDto(
    Guid MembershipId,
    Guid HouseholdId,
    Guid UserId,
    string? DisplayName,
    HouseholdRole Role,
    bool IsActive,
    string? InvitationCode,
    DateTime JoinedAt,
    DateTime UpdatedAt);
