using Bills.Application.Contracts;
using Bills.Application.Queries;
using Bills.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class HouseholdMembershipQuery : IHouseholdMembershipQuery
{
    private readonly BillsDbContext _db;

    public HouseholdMembershipQuery(BillsDbContext db) => _db = db;

    public async Task<IReadOnlyCollection<MembershipResponse>> ListMembersAsync(Guid householdId, CancellationToken cancellationToken = default)
    {
        var hid = HouseholdId.Create(householdId);
        var members = await _db.HouseholdMemberships
            .Where(m => m.HouseholdId == hid && m.IsActive)
            .ToListAsync(cancellationToken);

        var userIds = members.Select(m => m.UserId).ToList();
        var projections = await _db.UserProjections
            .Where(p => userIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);

        var projDict = projections.ToDictionary(p => p.UserId);

        return members.Select(m =>
        {
            projDict.TryGetValue(m.UserId, out var proj);
            return new MembershipResponse(
                m.Id.Value,
                m.HouseholdId.Value,
                m.UserId.Value,
                proj?.GetFullName(),
                m.Role,
                m.IsActive,
                m.InvitationCode,
                m.JoinedAt,
                m.UpdatedAt);
        }).ToList();
    }
}
