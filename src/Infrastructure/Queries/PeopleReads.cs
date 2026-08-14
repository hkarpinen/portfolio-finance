using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

/// <summary>
/// Who somebody is, and what they are in a house — read off the projections finance keeps of
/// identity and household.
///
/// These were written out four and two times respectively, in three different shapes: one
/// materialised every user in the database before filtering, one fetched a single row per share,
/// one built a name dictionary and one built a projection dictionary. Same join, four answers to
/// what to do when the projection is missing.
/// </summary>
internal static class PeopleReads
{
    /// <summary>Display names by user id. A user with no projection yet is simply absent — callers
    /// decide what to show, and every one of them already had to.</summary>
    public static async Task<Dictionary<Guid, string>> NamesAsync(
        FinanceDbContext db, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0) return [];

        return await db.UserProjections.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId.Value))
            .ToDictionaryAsync(p => p.UserId.Value, p => p.GetFullName(), ct);
    }

    /// <summary>Name and avatar together, for the places that show a face beside the name.</summary>
    public static async Task<Dictionary<Guid, (string Name, string? AvatarUrl)>> ProfilesAsync(
        FinanceDbContext db, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0) return [];

        return await db.UserProjections.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId.Value))
            .ToDictionaryAsync(p => p.UserId.Value, p => new ValueTuple<string, string?>(p.GetFullName(), p.AvatarUrl), ct);
    }

    /// <summary>
    /// Each member's role in one house. "Member" is the fallback for rows predating the membership
    /// projection — absent means unknown, not absent from the house.
    /// </summary>
    public static async Task<Dictionary<Guid, string>> RolesAsync(
        FinanceDbContext db, Guid groupId, CancellationToken ct = default)
        => await db.GroupMemberProjections.AsNoTracking()
            .Where(m => m.GroupId == groupId)
            .ToDictionaryAsync(m => m.UserId, m => m.Role, ct);
}
