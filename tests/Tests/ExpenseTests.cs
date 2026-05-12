using Finance.Domain.Aggregates;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Tests;

public class ExpenseTests
{
    private static Expense CreateValidExpense(
        UserId? userId = null,
        decimal amount = 75m,
        ExpenseCategory category = ExpenseCategory.Utilities,
        string title = "Phone Bill",
        RecurrenceSchedule? schedule = null)
    {
        return Expense.Create(
            userId ?? UserId.New(),
            title,
            Money.Create(amount, "USD"),
            category,
            DateTime.UtcNow.Date.AddDays(3),
            schedule);
    }

    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var userId = UserId.New();
        var dueDate = DateTime.UtcNow.Date.AddDays(7);
        var amount = Money.Create(120m, "USD");

        // Act
        var bill = Expense.Create(userId, "Netflix", amount, ExpenseCategory.Other, dueDate, description: "Streaming");

        // Assert
        Assert.Equal(userId, bill.UserId);
        Assert.Equal("Netflix", bill.Title);
        Assert.Equal(120m, bill.Amount.Amount);
        Assert.Equal(ExpenseCategory.Other, bill.Category);
        Assert.Equal(dueDate, bill.DueDate);
        Assert.Equal("Streaming", bill.Description);
        Assert.True(bill.IsActive);
        Assert.Null(bill.RecurrenceSchedule);
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
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() =>
            Expense.Create(UserId.New(), "  ", Money.Create(50m, "USD"), ExpenseCategory.Other, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Create_NegativeAmount_ShouldThrow()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() => Money.Create(-10m, "USD"));
    }

    [Fact]
    public void Create_WithRecurrenceSchedule_ShouldSetSchedule()
    {
        // Arrange
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, new DateTime(2024, 1, 1));

        // Act
        var bill = CreateValidExpense(schedule: schedule);

        // Assert
        Assert.NotNull(bill.RecurrenceSchedule);
        Assert.Equal(RecurrenceFrequency.Monthly, bill.RecurrenceSchedule.Frequency);
    }

    [Fact]
    public void Update_ShouldChangeTitleAmountCategoryAndDueDate()
    {
        // Arrange
        var bill = CreateValidExpense();
        bill.ClearDomainEvents();
        var newDueDate = DateTime.UtcNow.Date.AddDays(14);

        // Act
        bill.Update("Updated Bill", Money.Create(200m, "USD"), ExpenseCategory.Rent, newDueDate, description: "New desc");

        // Assert
        Assert.Equal("Updated Bill", bill.Title);
        Assert.Equal(200m, bill.Amount.Amount);
        Assert.Equal(ExpenseCategory.Rent, bill.Category);
        Assert.Equal(newDueDate, bill.DueDate);
        Assert.Equal("New desc", bill.Description);
    }

    [Fact]
    public void Update_ShouldRaise_ExpenseUpdatedEvent()
    {
        // Arrange
        var bill = CreateValidExpense();
        bill.ClearDomainEvents();

        // Act
        bill.Update("New Title", Money.Create(50m, "USD"), ExpenseCategory.Other, DateTime.UtcNow.Date.AddDays(5));

        // Assert
        Assert.Single(bill.GetDomainEvents());
        Assert.IsType<ExpenseUpdated>(bill.GetDomainEvents()[0]);
    }

    [Fact]
    public void Update_EmptyTitle_ShouldThrow()
    {
        // Arrange
        var bill = CreateValidExpense();

        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            bill.Update("", Money.Create(50m, "USD"), ExpenseCategory.Other, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Update_WithRecurrenceSchedule_ShouldUpdateSchedule()
    {
        // Arrange
        var bill = CreateValidExpense();
        var newSchedule = RecurrenceSchedule.Create(RecurrenceFrequency.Weekly, new DateTime(2024, 6, 1));

        // Act
        bill.Update("Title", Money.Create(50m, "USD"), ExpenseCategory.Other, DateTime.UtcNow.Date.AddDays(1), recurrenceSchedule: newSchedule);

        // Assert
        Assert.NotNull(bill.RecurrenceSchedule);
        Assert.Equal(RecurrenceFrequency.Weekly, bill.RecurrenceSchedule.Frequency);
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
    public void Deactivate_WhenAlreadyInactive_ShouldThrow()
    {
        // Arrange
        var bill = CreateValidExpense();
        bill.Deactivate();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => bill.Deactivate());
    }

    [Fact]
    public void TryDeactivate_WhenActive_ShouldReturnTrue_AndSetInactive()
    {
        // Arrange
        var bill = CreateValidExpense();

        // Act
        var result = bill.TryDeactivate();

        // Assert
        Assert.True(result);
        Assert.False(bill.IsActive);
    }

    [Fact]
    public void TryDeactivate_WhenAlreadyInactive_ShouldReturnFalse()
    {
        // Arrange
        var bill = CreateValidExpense();
        bill.Deactivate();

        // Act
        var result = bill.TryDeactivate();

        // Assert
        Assert.False(result);
        Assert.False(bill.IsActive);
    }

    [Fact]
    public void ClearDomainEvents_ShouldEmptyEvents()
    {
        // Arrange
        var bill = CreateValidExpense();
        Assert.NotEmpty(bill.GetDomainEvents());

        // Act
        bill.ClearDomainEvents();

        // Assert
        Assert.Empty(bill.GetDomainEvents());
    }
}
