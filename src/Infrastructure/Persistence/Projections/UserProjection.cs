using Finance.Domain.ValueObjects;

namespace Finance.Infrastructure.Persistence.Projections;

/// <summary>
/// Denormalized projection of user data synced from the Identity service via
/// domain events. Written by infrastructure event consumers; read by query classes.
/// Not a domain aggregate — has no lifecycle, invariants, or domain events.
/// Belongs in Infrastructure because its shape is driven by what queries need,
/// its persistence is managed by EF Core, and it is mutated by infrastructure
/// consumers, not by domain logic.
/// </summary>
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
}
