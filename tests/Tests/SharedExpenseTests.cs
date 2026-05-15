using Finance.Domain.Aggregates;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Tests;

public class SharedExpenseTests
{
    private static (GroupId, UserId) NewIds() => (GroupId.Create(Guid.NewGuid()), UserId.New());

    private static Expense CreateValidExpense(GroupId? groupId = null, UserId? createdBy = null)
    {
        var hId = groupId ?? GroupId.Create(Guid.NewGuid());
        var uId = createdBy ?? UserId.New();
        return Expense.CreateHousehold(
            hId,
            uId,
            "Test Bill",
            Money.Create(100m, "USD"),
            ExpenseCategory.Utilities,
            DateTime.UtcNow.Date.AddDays(1));
    }

    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var (hId, uId) = NewIds();
        var dueDate = DateTime.UtcNow.Date.AddDays(5);

        // Act
        var bill = Expense.CreateHousehold(hId, uId, "Electricity", Money.Create(80m, "USD"), ExpenseCategory.Utilities, dueDate);

        // Assert
        Assert.Equal("Electricity", bill.Title);
        Assert.Equal(hId, bill.GroupId);
        Assert.Equal(uId, bill.CreatedBy);
        Assert.Equal(dueDate, bill.DueDate);
        Assert.True(bill.IsActive);
    }

    [Fact]
    public void Create_ShouldRaise_ExpenseCreatedEvent()
    {
        // Arrange / Act
        var bill = CreateValidExpense();

        // Assert
        Assert.Single(bill.GetDomainEvents());
        Assert.IsType<ExpenseCreated>(bill.GetDomainEvents()[0]);
    }

    [Fact]
    public void Create_EmptyTitle_ShouldThrow()
    {
        // Arrange
        var (hId, uId) = NewIds();

        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            Expense.CreateHousehold(hId, uId, "", Money.Create(100m, "USD"), ExpenseCategory.Other, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        // Arrange
        var bill = CreateValidExpense();

        // Act
        bill.Deactivate();

        // Assert
        Assert.False(bill.IsActive);
    }

    [Fact]
    public void Deactivate_AlreadyInactive_ShouldThrow()
    {
        // Arrange
        var bill = CreateValidExpense();
        bill.Deactivate();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => bill.Deactivate());
    }

    [Fact]
    public void Deactivate_ShouldRaise_ExpenseDeactivatedEvent()
    {
        // Arrange
        var bill = CreateValidExpense();
        bill.ClearDomainEvents();

        // Act
        bill.Deactivate();

        // Assert
        Assert.Single(bill.GetDomainEvents());
        Assert.IsType<ExpenseDeactivated>(bill.GetDomainEvents()[0]);
    }

    [Fact]
    public void Update_ShouldChangeTitleAndAmount()
    {
        // Arrange
        var bill = CreateValidExpense();
        bill.ClearDomainEvents();
        var newDueDate = DateTime.UtcNow.Date.AddDays(10);

        // Act
        bill.Update("Updated Title", Money.Create(200m, "USD"), ExpenseCategory.Rent, newDueDate);

        // Assert
        Assert.Equal("Updated Title", bill.Title);
        Assert.Equal(200m, bill.Amount.Amount);
        Assert.Equal(ExpenseCategory.Rent, bill.Category);
    }
}
