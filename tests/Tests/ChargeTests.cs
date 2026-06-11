using Finance.Domain.Aggregates;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Tests;

public class ChargeTests
{
    private static Charge CreateValidCharge(
        UserId? userId = null,
        decimal amount = 75m,
        ChargeCategory category = ChargeCategory.Utilities,
        string title = "Phone Bill",
        RecurrenceSchedule? schedule = null)
    {
        return Charge.Create(
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
        var bill = Charge.Create(userId, "Netflix", amount, ChargeCategory.Other, dueDate, description: "Streaming");

        // Assert
        Assert.Equal(userId, bill.UserId);
        Assert.Equal("Netflix", bill.Title);
        Assert.Equal(120m, bill.Amount.Amount);
        Assert.Equal(ChargeCategory.Other, bill.Category);
        Assert.Equal(dueDate, bill.DueDate);
        Assert.Equal("Streaming", bill.Description);
        Assert.True(bill.IsActive);
        Assert.Null(bill.RecurrenceSchedule);
    }

    [Fact]
    public void Create_ShouldRaise_ChargeCreatedEvent()
    {
        // Arrange / Act
        var bill = CreateValidCharge();

        // Assert
        Assert.Single(bill.GetDomainEvents());
        Assert.IsType<ChargeCreated>(bill.GetDomainEvents()[0]);
    }

    [Fact]
    public void Create_EmptyTitle_ShouldThrow()
    {
        // Arrange / Act / Assert
        Assert.Throws<ArgumentException>(() =>
            Charge.Create(UserId.New(), "  ", Money.Create(50m, "USD"), ChargeCategory.Other, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Create_NegativeAmount_ShouldThrow()
    {
        // Money is signed now (refunds, contra entries, inflows). The non-negative
        // invariant for an expense lives on the Charge aggregate, not on Money.
        Assert.Throws<ArgumentException>(() =>
            Charge.Create(UserId.New(), "Rent", Money.Create(-10m, "USD"), ChargeCategory.Other, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Create_WithRecurrenceSchedule_ShouldSetSchedule()
    {
        // Arrange
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, new DateTime(2024, 1, 1));

        // Act
        var bill = CreateValidCharge(schedule: schedule);

        // Assert
        Assert.NotNull(bill.RecurrenceSchedule);
        Assert.Equal(RecurrenceFrequency.Monthly, bill.RecurrenceSchedule.Frequency);
    }

    [Fact]
    public void Update_ShouldChangeTitleAmountCategoryAndDueDate()
    {
        // Arrange
        var bill = CreateValidCharge();
        bill.ClearDomainEvents();
        var newDueDate = DateTime.UtcNow.Date.AddDays(14);

        // Act
        bill.Update("Updated Bill", Money.Create(200m, "USD"), ChargeCategory.Rent, newDueDate, description: "New desc");

        // Assert
        Assert.Equal("Updated Bill", bill.Title);
        Assert.Equal(200m, bill.Amount.Amount);
        Assert.Equal(ChargeCategory.Rent, bill.Category);
        Assert.Equal(newDueDate, bill.DueDate);
        Assert.Equal("New desc", bill.Description);
    }

    [Fact]
    public void Update_ShouldRaise_ChargeUpdatedEvent()
    {
        // Arrange
        var bill = CreateValidCharge();
        bill.ClearDomainEvents();

        // Act
        bill.Update("New Title", Money.Create(50m, "USD"), ChargeCategory.Other, DateTime.UtcNow.Date.AddDays(5));

        // Assert
        Assert.Single(bill.GetDomainEvents());
        Assert.IsType<ChargeUpdated>(bill.GetDomainEvents()[0]);
    }

    [Fact]
    public void Update_EmptyTitle_ShouldThrow()
    {
        // Arrange
        var bill = CreateValidCharge();

        // Act / Assert
        Assert.Throws<ArgumentException>(() =>
            bill.Update("", Money.Create(50m, "USD"), ChargeCategory.Other, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Update_WithRecurrenceSchedule_ShouldUpdateSchedule()
    {
        // Arrange
        var bill = CreateValidCharge();
        var newSchedule = RecurrenceSchedule.Create(RecurrenceFrequency.Weekly, new DateTime(2024, 6, 1));

        // Act
        bill.Update("Title", Money.Create(50m, "USD"), ChargeCategory.Other, DateTime.UtcNow.Date.AddDays(1), recurrenceSchedule: newSchedule);

        // Assert
        Assert.NotNull(bill.RecurrenceSchedule);
        Assert.Equal(RecurrenceFrequency.Weekly, bill.RecurrenceSchedule.Frequency);
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        // Arrange
        var bill = CreateValidCharge();

        // Act
        bill.Deactivate();

        // Assert
        Assert.False(bill.IsActive);
    }

    [Fact]
    public void Deactivate_ShouldRaise_ChargeDeactivatedEvent()
    {
        // Arrange
        var bill = CreateValidCharge();
        bill.ClearDomainEvents();

        // Act
        bill.Deactivate();

        // Assert
        Assert.Single(bill.GetDomainEvents());
        Assert.IsType<ChargeDeactivated>(bill.GetDomainEvents()[0]);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldThrow()
    {
        // Arrange
        var bill = CreateValidCharge();
        bill.Deactivate();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => bill.Deactivate());
    }

    [Fact]
    public void TryDeactivate_WhenActive_ShouldReturnTrue_AndSetInactive()
    {
        // Arrange
        var bill = CreateValidCharge();

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
        var bill = CreateValidCharge();
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
        var bill = CreateValidCharge();
        Assert.NotEmpty(bill.GetDomainEvents());

        // Act
        bill.ClearDomainEvents();

        // Assert
        Assert.Empty(bill.GetDomainEvents());
    }
}
