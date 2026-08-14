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
        string title = "Phone Bill")
    {
        return Expense.CreateOwn(
            userId ?? UserId.New(),
            title,
            Money.Create(amount, "USD"),
            category,
            DateTime.UtcNow.Date.AddDays(3));
    }

    [Fact]
    public void Create_ShouldSetProperties()
    {
        var userId = UserId.New();
        var dueDate = DateTime.UtcNow.Date.AddDays(7);
        var amount = Money.Create(120m, "USD");

        var bill = Expense.CreateOwn(userId, "Netflix", amount, ExpenseCategory.Other, dueDate, description: "Streaming");

        Assert.Equal(userId, bill.EnteredBy);
        Assert.Equal("Netflix", bill.Title);
        Assert.Equal(120m, bill.Amount.Amount);
        Assert.Equal(ExpenseCategory.Other, bill.Category);
        Assert.Equal(dueDate, bill.DueDate);
        Assert.Equal("Streaming", bill.Description);
        Assert.True(bill.IsActive);
    }

    [Fact]
    public void Create_ShouldRaise_ExpenseCreatedEvent()
    {
        var bill = CreateValidExpense();

        Assert.Single(bill.GetDomainEvents());
        Assert.IsType<ExpenseCreated>(bill.GetDomainEvents()[0]);
    }

    [Fact]
    public void Create_EmptyTitle_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            Expense.CreateOwn(UserId.New(), "  ", Money.Create(50m, "USD"), ExpenseCategory.Other, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Create_NegativeAmount_ShouldThrow()
    {
        // Money is signed now (refunds, contra entries, inflows). The non-negative
        // invariant for an expense lives on the Expense aggregate, not on Money.
        Assert.Throws<ArgumentException>(() =>
            Expense.CreateOwn(UserId.New(), "Rent", Money.Create(-10m, "USD"), ExpenseCategory.Other, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Update_ShouldChangeTitleAmountCategoryAndDueDate()
    {
        var bill = CreateValidExpense();
        bill.ClearDomainEvents();
        var newDueDate = DateTime.UtcNow.Date.AddDays(14);

        bill.Update("Updated Bill", Money.Create(200m, "USD"), ExpenseCategory.Rent, newDueDate, description: "New desc");

        Assert.Equal("Updated Bill", bill.Title);
        Assert.Equal(200m, bill.Amount.Amount);
        Assert.Equal(ExpenseCategory.Rent, bill.Category);
        Assert.Equal(newDueDate, bill.DueDate);
        Assert.Equal("New desc", bill.Description);
    }

    [Fact]
    public void Update_ShouldRaise_ExpenseUpdatedEvent()
    {
        var bill = CreateValidExpense();
        bill.ClearDomainEvents();

        bill.Update("New Title", Money.Create(50m, "USD"), ExpenseCategory.Other, DateTime.UtcNow.Date.AddDays(5));

        Assert.Single(bill.GetDomainEvents());
        Assert.IsType<ExpenseUpdated>(bill.GetDomainEvents()[0]);
    }

    [Fact]
    public void Update_EmptyTitle_ShouldThrow()
    {
        var bill = CreateValidExpense();

        Assert.Throws<ArgumentException>(() =>
            bill.Update("", Money.Create(50m, "USD"), ExpenseCategory.Other, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var bill = CreateValidExpense();

        bill.Deactivate();

        Assert.False(bill.IsActive);
    }

    [Fact]
    public void Deactivate_ShouldRaise_ExpenseDeactivatedEvent()
    {
        var bill = CreateValidExpense();
        bill.ClearDomainEvents();

        bill.Deactivate();

        Assert.Single(bill.GetDomainEvents());
        Assert.IsType<ExpenseDeactivated>(bill.GetDomainEvents()[0]);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldThrow()
    {
        var bill = CreateValidExpense();
        bill.Deactivate();

        Assert.Throws<InvalidOperationException>(() => bill.Deactivate());
    }

    [Fact]
    public void TryDeactivate_WhenActive_ShouldReturnTrue_AndSetInactive()
    {
        var bill = CreateValidExpense();

        var result = bill.TryDeactivate();

        Assert.True(result);
        Assert.False(bill.IsActive);
    }

    [Fact]
    public void TryDeactivate_WhenAlreadyInactive_ShouldReturnFalse()
    {
        var bill = CreateValidExpense();
        bill.Deactivate();

        var result = bill.TryDeactivate();

        Assert.False(result);
        Assert.False(bill.IsActive);
    }

    [Fact]
    public void ClearDomainEvents_ShouldEmptyEvents()
    {
        var bill = CreateValidExpense();
        Assert.NotEmpty(bill.GetDomainEvents());

        bill.ClearDomainEvents();

        Assert.Empty(bill.GetDomainEvents());
    }
}
