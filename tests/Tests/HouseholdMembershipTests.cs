using Bills.Domain.Aggregates;
using Bills.Domain.Events;
using Bills.Domain.ValueObjects;

namespace Tests;

public class HouseholdMembershipTests
{
    private static HouseholdMembership CreateActiveMembership(HouseholdRole role = HouseholdRole.Member)
        => HouseholdMembership.Create(HouseholdId.New(), UserId.New(), role);

    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var householdId = HouseholdId.New();
        var userId = UserId.New();

        // Act
        var membership = HouseholdMembership.Create(householdId, userId, HouseholdRole.Owner);

        // Assert
        Assert.Equal(householdId, membership.HouseholdId);
        Assert.Equal(userId, membership.UserId);
        Assert.Equal(HouseholdRole.Owner, membership.Role);
        Assert.True(membership.IsActive);
        Assert.Null(membership.InvitationCode);
    }

    [Fact]
    public void Create_ShouldRaise_HouseholdMemberJoinedEvent()
    {
        var membership = CreateActiveMembership();
        Assert.Single(membership.GetDomainEvents());
        Assert.IsType<HouseholdMemberJoined>(membership.GetDomainEvents()[0]);
    }

    [Fact]
    public void CreateWithInvitation_ShouldBeInactive_AndHaveInvitationCode()
    {
        // Arrange
        var householdId = HouseholdId.New();
        var invitedBy = UserId.New();

        // Act
        var membership = HouseholdMembership.CreateWithInvitation(householdId, invitedBy, "INV-123");

        // Assert
        Assert.False(membership.IsActive);
        Assert.Equal("INV-123", membership.InvitationCode);
    }

    [Fact]
    public void CreateWithInvitation_ShouldRaise_HouseholdMemberInvitedEvent()
    {
        var membership = HouseholdMembership.CreateWithInvitation(HouseholdId.New(), UserId.New(), "INV-1");
        Assert.Single(membership.GetDomainEvents());
        Assert.IsType<HouseholdMemberInvited>(membership.GetDomainEvents()[0]);
    }

    [Fact]
    public void ChangeRole_ShouldUpdateRole_AndRaiseEvent()
    {
        var membership = CreateActiveMembership(HouseholdRole.Member);
        membership.ClearDomainEvents();

        membership.ChangeRole(HouseholdRole.Owner);

        Assert.Equal(HouseholdRole.Owner, membership.Role);
        Assert.Single(membership.GetDomainEvents());
        Assert.IsType<HouseholdMemberRoleChanged>(membership.GetDomainEvents()[0]);
    }

    [Fact]
    public void ChangeRole_SameRole_ShouldThrow()
    {
        var membership = CreateActiveMembership(HouseholdRole.Member);
        Assert.Throws<InvalidOperationException>(() => membership.ChangeRole(HouseholdRole.Member));
    }

    [Fact]
    public void Remove_ShouldSetInactive_AndRaiseEvent()
    {
        var membership = CreateActiveMembership();
        membership.ClearDomainEvents();

        membership.Remove();

        Assert.False(membership.IsActive);
        Assert.Single(membership.GetDomainEvents());
        Assert.IsType<HouseholdMemberRemoved>(membership.GetDomainEvents()[0]);
    }

    [Fact]
    public void Remove_WhenAlreadyInactive_ShouldThrow()
    {
        var membership = CreateActiveMembership();
        membership.Remove();
        Assert.Throws<InvalidOperationException>(() => membership.Remove());
    }

    [Fact]
    public void AcceptInvitation_ShouldActivateMembership_AndClearCode()
    {
        // Arrange
        var membership = HouseholdMembership.CreateWithInvitation(HouseholdId.New(), UserId.New(), "INV-1");
        membership.ClearDomainEvents();
        var joiningUser = UserId.New();

        // Act
        membership.AcceptInvitation(joiningUser);

        // Assert
        Assert.True(membership.IsActive);
        Assert.Equal(joiningUser, membership.UserId);
        Assert.Null(membership.InvitationCode);
        Assert.Single(membership.GetDomainEvents());
        Assert.IsType<HouseholdMemberJoined>(membership.GetDomainEvents()[0]);
    }

    [Fact]
    public void AcceptInvitation_WhenAlreadyActive_ShouldThrow()
    {
        var membership = CreateActiveMembership();
        Assert.Throws<InvalidOperationException>(() => membership.AcceptInvitation(UserId.New()));
    }
}
