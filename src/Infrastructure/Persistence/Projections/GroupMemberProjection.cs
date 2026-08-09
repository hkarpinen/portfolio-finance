namespace Finance.Infrastructure.Persistence.Projections;

// Denormalized projection of group membership synced from the household service via domain
// events. Note the deliberate asymmetry with the ledger: when a member leaves, this row goes
// inactive but their Member account and balances stay on the books — debt does not vanish with
// departure. Telling current members from departed ones is what this projection is for.
public sealed class GroupMemberProjection
{
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = "Member";
    public bool IsActive { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    private GroupMemberProjection() { }

    public static GroupMemberProjection Create(Guid groupId, Guid userId, string role, DateTime joinedAt)
    {
        return new GroupMemberProjection
        {
            GroupId = groupId,
            UserId = userId,
            Role = role,
            IsActive = true,
            JoinedAt = joinedAt,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public void Rejoin(string role, DateTime joinedAt)
    {
        Role = role;
        IsActive = true;
        JoinedAt = joinedAt;
        LeftAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Depart(DateTime leftAt)
    {
        IsActive = false;
        LeftAt = leftAt;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ChangeRole(string newRole)
    {
        Role = newRole;
        UpdatedAt = DateTime.UtcNow;
    }
}
