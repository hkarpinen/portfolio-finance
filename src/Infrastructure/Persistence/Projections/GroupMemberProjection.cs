namespace Finance.Infrastructure.Persistence.Projections;

/// <summary>
/// Denormalized projection of group (household) membership synced from the household service via
/// domain events — who is in a group and with what role. Written by infrastructure event
/// consumers; read by query classes (e.g. to label an allocation with the member's role and to
/// tell current members from departed ones). Not a domain aggregate — no lifecycle, invariants,
/// or domain events. Note the deliberate asymmetry with the ledger: when a member leaves, this
/// row goes inactive but their Member account and balances stay on the books — debt does not
/// vanish with departure.
/// </summary>
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
