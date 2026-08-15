using Finance.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace Finance.Infrastructure.Persistence.Projections;

public sealed class UserProjection
{
    public UserId UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsActive { get; set; }
    public bool IsDemo { get; set; }

    private UserProjection() { }

    public static UserProjection Create(UserId userId, string email, string firstName, string lastName, string? avatarUrl = null)
    {
        return new UserProjection
        {
            UserId = userId,
            Email = email ?? throw new ArgumentNullException(nameof(email)),
            FirstName = firstName ?? throw new ArgumentNullException(nameof(firstName)),
            LastName = lastName ?? throw new ArgumentNullException(nameof(lastName)),
            AvatarUrl = avatarUrl,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    public string GetFullName() => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Display names by user id. Written out four times in the read side, in three shapes, one of
    /// which materialised every projection in the database before filtering — and each with its own
    /// answer for a user with no projection yet. Absent here, and callers decide what to show.
    /// </summary>
    public static async Task<Dictionary<Guid, string>> NamesAsync(
        DbSet<UserProjection> projections, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0) return [];

        // Compare the property EF knows, not `.Value` through it: UserId is value-converted, so
        // the converted member translates and reaching inside it does not — the query then throws
        // when it runs rather than failing to compile.
        var ids = userIds.Select(id => new UserId(id)).ToList();

        return await projections.AsNoTracking()
            .Where(p => ids.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId.Value, p => p.GetFullName(), ct);
    }

    /// <summary>Name and avatar together, for the places that show a face beside the name.</summary>
    public static async Task<Dictionary<Guid, (string Name, string? AvatarUrl)>> ProfilesAsync(
        DbSet<UserProjection> projections, IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
    {
        if (userIds.Count == 0) return [];

        var ids = userIds.Select(id => new UserId(id)).ToList();

        return await projections.AsNoTracking()
            .Where(p => ids.Contains(p.UserId))
            .ToDictionaryAsync(
                p => p.UserId.Value,
                p => new ValueTuple<string, string?>(p.GetFullName(), p.AvatarUrl), ct);
    }
}
