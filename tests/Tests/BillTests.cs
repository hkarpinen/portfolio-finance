using Finance.Domain.Aggregates;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Tests;

public class BillTests
{
    private static (HouseholdId, UserId) NewIds() => (HouseholdId.New(), UserId.New());

    private static Bill CreateValidBill(HouseholdId? householdId = null, UserId? createdBy = null)
    {
        var hId = householdId ?? HouseholdId.New();
        var uId = createdBy ?? UserId.New();
        return Bill.Create(
            hId,
            "Test Bill",
            Money.Create(100m, "USD"),
            BillCategory.Utilities,
            uId,
            DateTime.UtcNow.Date.AddDays(1));
    }

    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var (hId, uId) = NewIds();
        var dueDate = DateTime.UtcNow.Date.AddDays(5);

        // Act
        var bill = Bill.Create(hId, "Electricity", Money.Create(80m, "USD"), BillCategory.Utilities, uId, dueDate);

        // Assert
        Assert.Equal("Electricity", bill.Title);
        Assert.Equal(hId, bill.HouseholdId);
        Assert.Equal(uId, bill.CreatedBy);
        Assert.Equal(dueDate, bill.DueDate);
        Assert.True(bill.IsActive);
    }

    [Fact]
    public void Create_ShouldRaise_BillCreatedEvent()
    {
        // Arrange / Act
        var bill = CreateValidBill();

        // Assert
        Assert.Single(bill.GetDomainEvents());
        Assert.IsType<BillCreated>(bill.GetDomainEvents()[0]);
    }

    [Fact]
    public void Create_EmptyTitle_ShouldThrow()
    {
        // Arrange
        var (hId, uId) = NewIds();

        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            Bill.Create(hId, "", Money.Create(100m, "USD"), BillCategory.Other, uId, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        // Arrange
        var bill = CreateValidBill();

        // Act
        bill.Deactivate();

        // Assert
        Assert.False(bill.IsActive);
    }

    [Fact]
    public void Deactivate_AlreadyInactive_ShouldThrow()
    {
        // Arrange
        var bill = CreateValidBill();
        bill.Deactivate();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => bill.Deactivate());
    }

    [Fact]
    public void Deactivate_ShouldRaise_BillDeactivatedEvent()
    {
        // Arrange
        var bill = CreateValidBill();
        bill.ClearDomainEvents();

        // Act
        bill.Deactivate();

        // Assert
        Assert.Single(bill.GetDomainEvents());
        Assert.IsType<BillDeactivated>(bill.GetDomainEvents()[0]);
    }

    [Fact]
    public void Update_ShouldChangeTitleAndAmount()
    {
        // Arrange
        var bill = CreateValidBill();
        bill.ClearDomainEvents();
        var newDueDate = DateTime.UtcNow.Date.AddDays(10);

        // Act
        bill.Update("Updated Title", Money.Create(200m, "USD"), BillCategory.Rent, newDueDate);

        // Assert
        Assert.Equal("Updated Title", bill.Title);
        Assert.Equal(200m, bill.Amount.Amount);
        Assert.Equal(BillCategory.Rent, bill.Category);
    }
}
