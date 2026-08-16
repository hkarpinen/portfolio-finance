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
        string title = "Phone Expense")
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

        var expense = Expense.CreateOwn(userId, "Netflix", amount, ExpenseCategory.Other, dueDate, description: "Streaming");

        Assert.Equal(userId, expense.EnteredBy);
        Assert.Equal("Netflix", expense.Title);
        Assert.Equal(120m, expense.Amount.Amount);
        Assert.Equal(ExpenseCategory.Other, expense.Category);
        Assert.Equal(dueDate, expense.DueDate);
        Assert.Equal("Streaming", expense.Description);
        Assert.True(expense.IsActive);
    }

    [Fact]
    public void Create_ShouldRaise_ExpenseCreatedEvent()
    {
        var expense = CreateValidExpense();

        Assert.Single(expense.GetDomainEvents());
        Assert.IsType<ExpenseCreated>(expense.GetDomainEvents()[0]);
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
        var expense = CreateValidExpense();
        expense.ClearDomainEvents();
        var newDueDate = DateTime.UtcNow.Date.AddDays(14);

        expense.Update("Updated Expense", Money.Create(200m, "USD"), ExpenseCategory.Rent, newDueDate, description: "New desc");

        Assert.Equal("Updated Expense", expense.Title);
        Assert.Equal(200m, expense.Amount.Amount);
        Assert.Equal(ExpenseCategory.Rent, expense.Category);
        Assert.Equal(newDueDate, expense.DueDate);
        Assert.Equal("New desc", expense.Description);
    }

    [Fact]
    public void Update_ShouldRaise_ExpenseUpdatedEvent()
    {
        var expense = CreateValidExpense();
        expense.ClearDomainEvents();

        expense.Update("New Title", Money.Create(50m, "USD"), ExpenseCategory.Other, DateTime.UtcNow.Date.AddDays(5));

        Assert.Single(expense.GetDomainEvents());
        Assert.IsType<ExpenseUpdated>(expense.GetDomainEvents()[0]);
    }

    [Fact]
    public void Update_EmptyTitle_ShouldThrow()
    {
        var expense = CreateValidExpense();

        Assert.Throws<ArgumentException>(() =>
            expense.Update("", Money.Create(50m, "USD"), ExpenseCategory.Other, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var expense = CreateValidExpense();

        expense.Deactivate();

        Assert.False(expense.IsActive);
    }

    [Fact]
    public void Deactivate_ShouldRaise_ExpenseDeactivatedEvent()
    {
        var expense = CreateValidExpense();
        expense.ClearDomainEvents();

        expense.Deactivate();

        Assert.Single(expense.GetDomainEvents());
        Assert.IsType<ExpenseDeactivated>(expense.GetDomainEvents()[0]);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldThrow()
    {
        var expense = CreateValidExpense();
        expense.Deactivate();

        Assert.Throws<InvalidOperationException>(() => expense.Deactivate());
    }

    [Fact]
    public void TryDeactivate_WhenActive_ShouldReturnTrue_AndSetInactive()
    {
        var expense = CreateValidExpense();

        var result = expense.TryDeactivate();

        Assert.True(result);
        Assert.False(expense.IsActive);
    }

    [Fact]
    public void TryDeactivate_WhenAlreadyInactive_ShouldReturnFalse()
    {
        var expense = CreateValidExpense();
        expense.Deactivate();

        var result = expense.TryDeactivate();

        Assert.False(result);
        Assert.False(expense.IsActive);
    }

    [Fact]
    public void ClearDomainEvents_ShouldEmptyEvents()
    {
        var expense = CreateValidExpense();
        Assert.NotEmpty(expense.GetDomainEvents());

        expense.ClearDomainEvents();

        Assert.Empty(expense.GetDomainEvents());
    }
}
