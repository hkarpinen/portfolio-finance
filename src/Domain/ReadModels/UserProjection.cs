using Finance.Domain.ValueObjects;

namespace Finance.Domain.ReadModels;

/// <summary>
/// Denormalized read model for user data projected from the Identity service.
/// Not an aggregate — has no lifecycle, invariants, or domain events.
/// Mutation is performed by infrastructure consumers only.
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
