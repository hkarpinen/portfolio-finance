using Finance.Application.Contracts;
using Finance.Domain.Aggregates;

namespace Finance.Application.Mappers;

public static class MembershipMapper
{
    public static MembershipResponse ToResponse(HouseholdMembership membership, string? fullName = null) => new(
        membership.Id.Value,
        membership.HouseholdId.Value,
        membership.UserId.Value,
        fullName,
        membership.Role,
        membership.IsActive,
        membership.InvitationCode,
        membership.JoinedAt,
        membership.UpdatedAt);
}
