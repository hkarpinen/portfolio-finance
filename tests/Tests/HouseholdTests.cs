using Finance.Domain.Aggregates;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Tests;

public class HouseholdTests
{
    private static UserId NewUserId() => UserId.New();

    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var ownerId = NewUserId();

        // Act
        var household = Household.Create("My Household", ownerId, "USD", "A description");

        // Assert
        Assert.Equal("My Household", household.Name);
        Assert.Equal(ownerId, household.OwnerId);
        Assert.Equal("USD", household.CurrencyCode);
        Assert.Equal("A description", household.Description);
        Assert.True(household.IsActive);
    }

    [Fact]
    public void Create_ShouldRaise_HouseholdCreatedEvent()
    {
        // Arrange / Act
        var household = Household.Create("Test", NewUserId());

        // Assert
        Assert.Single(household.GetDomainEvents());
        Assert.IsType<HouseholdCreated>(household.GetDomainEvents()[0]);
    }

    [Fact]
    public void Update_ShouldRenamHousehold()
    {
        // Arrange
        var household = Household.Create("Old Name", NewUserId());
        household.ClearDomainEvents();

        // Act
        household.Update("New Name", "New description");

        // Assert
        Assert.Equal("New Name", household.Name);
        Assert.Equal("New description", household.Description);
    }

    [Fact]
    public void Update_EmptyName_ShouldThrow()
    {
        // Arrange
        var household = Household.Create("Name", NewUserId());

        // Act / Assert
        Assert.Throws<ArgumentException>(() => household.Update(""));
    }

    [Fact]
    public void Update_ShouldRaise_HouseholdUpdatedEvent()
    {
        // Arrange
        var household = Household.Create("Name", NewUserId());
        household.ClearDomainEvents();

        // Act
        household.Update("New Name");

        // Assert
        Assert.Single(household.GetDomainEvents());
        Assert.IsType<HouseholdUpdated>(household.GetDomainEvents()[0]);
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        // Arrange
        var household = Household.Create("Name", NewUserId());

        // Act
        household.Deactivate();

        // Assert
        Assert.False(household.IsActive);
    }

    [Fact]
    public void Deactivate_AlreadyInactive_ShouldThrow()
    {
        // Arrange
        var household = Household.Create("Name", NewUserId());
        household.Deactivate();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => household.Deactivate());
    }

    [Fact]
    public void Deactivate_ShouldRaise_HouseholdDeletedEvent()
    {
        // Arrange
        var household = Household.Create("Name", NewUserId());
        household.ClearDomainEvents();

        // Act
        household.Deactivate();

        // Assert
        Assert.Single(household.GetDomainEvents());
        Assert.IsType<HouseholdDeleted>(household.GetDomainEvents()[0]);
    }
}
