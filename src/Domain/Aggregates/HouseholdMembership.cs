using Bills.Domain.Events;
using Bills.Domain.ValueObjects;

namespace Bills.Domain.Aggregates;

/// <summary>
/// Household membership aggregate root representing a member's participation in a household.
/// </summary>
public class HouseholdMembership
{
    private readonly List<DomainEvent> _domainEvents = new();

    public MembershipId Id { get; private set; }
    public HouseholdId HouseholdId { get; private set; }
    public UserId UserId { get; private set; }
    public HouseholdRole Role { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsActive { get; private set; }
    public string? InvitationCode { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();

    public void ClearDomainEvents() => _domainEvents.Clear();

    private HouseholdMembership()
    {
    }

    public static HouseholdMembership Create(HouseholdId householdId, UserId userId, HouseholdRole role = HouseholdRole.Member)
    {
        var membership = new HouseholdMembership
        {
            Id = MembershipId.New(),
            HouseholdId = householdId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true,
            InvitationCode = null
        };

        membership._domainEvents.Add(new HouseholdMemberJoined(membership.Id, householdId, userId, role));

        return membership;
    }

    public static HouseholdMembership CreateWithInvitation(HouseholdId householdId, UserId invitedBy, string invitationCode)
    {
        var membership = new HouseholdMembership
        {
            Id = MembershipId.New(),
            HouseholdId = householdId,
            UserId = UserId.Create(Guid.Empty), // Will be set when member joins
            Role = HouseholdRole.Member,
            JoinedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = false,
            InvitationCode = invitationCode
        };

        membership._domainEvents.Add(new HouseholdMemberInvited(membership.Id, householdId, invitedBy, invitationCode));

        return membership;
    }

    public void ChangeRole(HouseholdRole newRole)
    {
        if (newRole == Role)
            throw new InvalidOperationException("New role is the same as current role.");

        Role = newRole;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new HouseholdMemberRoleChanged(Id, HouseholdId, UserId, newRole));
    }

    public void Remove()
    {
        if (!IsActive)
            throw new InvalidOperationException("Membership is already inactive.");

        IsActive = false;
        UpdatedAt = DateTime.UtcNow;

        _domainEvents.Add(new HouseholdMemberRemoved(Id, HouseholdId, UserId));
    }

    public void AcceptInvitation(UserId userId)
    {
        if (IsActive)
            throw new InvalidOperationException("Membership is already active.");

        if (string.IsNullOrEmpty(InvitationCode))
            throw new InvalidOperationException("Membership was not invited.");

        UserId = userId;
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
        InvitationCode = null;

        _domainEvents.Add(new HouseholdMemberJoined(Id, HouseholdId, userId, Role));
    }
}
