using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Tests;

public class ExpenseAggregateTests
{
    private static UserId NewUser() => UserId.Create(Guid.NewGuid());
    private static Money Usd(decimal amount) => Money.Create(amount, "USD");

    // ── Create (personal) ────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidPersonalExpense_SetsProperties()
    {
        var userId = NewUser();
        var due = new DateTime(2026, 6, 1);
        var expense = Expense.Create(userId, "Netflix", Usd(15.99m), ExpenseCategory.Other, due);

        Assert.Equal(userId, expense.UserId);
        Assert.Equal("Netflix", expense.Title);
        Assert.Equal(15.99m, expense.Amount.Amount);
        Assert.Null(expense.HouseholdId);
        Assert.Null(expense.CreatedBy);
        Assert.True(expense.IsActive);
        Assert.Single(expense.GetDomainEvents());
    }

    [Fact]
    public void Create_WithRecurrenceSchedule_SetsSchedule()
    {
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, new DateTime(2026, 1, 1));
        var expense = Expense.Create(NewUser(), "Gym", Usd(50m), ExpenseCategory.Other, DateTime.UtcNow, schedule);

        Assert.NotNull(expense.RecurrenceSchedule);
        Assert.Equal(RecurrenceFrequency.Monthly, expense.RecurrenceSchedule.Frequency);
    }

    [Fact]
    public void Create_EmptyTitle_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Expense.Create(NewUser(), "", Usd(10m), ExpenseCategory.Other, DateTime.UtcNow));
    }

    [Fact]
    public void Create_NegativeAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Expense.Create(NewUser(), "Bad", Usd(-1m), ExpenseCategory.Other, DateTime.UtcNow));
    }

    // ── CreateHousehold ──────────────────────────────────────────────────────

    [Fact]
    public void CreateHousehold_SetsHouseholdIdAndCreatedBy()
    {
        var hId = HouseholdId.Create(Guid.NewGuid());
        var creator = NewUser();
        var expense = Expense.CreateHousehold(hId, creator, "Rent", Usd(1200m), ExpenseCategory.Other, DateTime.UtcNow);

        Assert.Equal(hId, expense.HouseholdId);
        Assert.Equal(creator, expense.CreatedBy);
        Assert.Equal(creator, expense.UserId);
        Assert.True(expense.IsActive);
        Assert.Single(expense.GetDomainEvents());
    }

    // ── Update ───────────────────────────────────────────────────────────────

    [Fact]
    public void Update_ChangesProperties()
    {
        var expense = Expense.Create(NewUser(), "Old", Usd(10m), ExpenseCategory.Other, DateTime.UtcNow);
        expense.ClearDomainEvents();

        expense.Update("New", Usd(20m), ExpenseCategory.Other, DateTime.UtcNow.AddDays(5));

        Assert.Equal("New", expense.Title);
        Assert.Equal(20m, expense.Amount.Amount);
        Assert.Single(expense.GetDomainEvents());
    }

    [Fact]
    public void Update_EmptyTitle_Throws()
    {
        var expense = Expense.Create(NewUser(), "Valid", Usd(10m), ExpenseCategory.Other, DateTime.UtcNow);
        Assert.Throws<ArgumentException>(() =>
            expense.Update("", Usd(10m), ExpenseCategory.Other, DateTime.UtcNow));
    }

    // ── Deactivate / Activate ────────────────────────────────────────────────

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var expense = Expense.Create(NewUser(), "Sub", Usd(5m), ExpenseCategory.Other, DateTime.UtcNow);
        expense.Deactivate();
        Assert.False(expense.IsActive);
    }

    [Fact]
    public void Deactivate_AlreadyInactive_Throws()
    {
        var expense = Expense.Create(NewUser(), "Sub", Usd(5m), ExpenseCategory.Other, DateTime.UtcNow);
        expense.Deactivate();
        Assert.Throws<InvalidOperationException>(() => expense.Deactivate());
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var expense = Expense.Create(NewUser(), "Sub", Usd(5m), ExpenseCategory.Other, DateTime.UtcNow);
        expense.Deactivate();
        expense.Activate();
        Assert.True(expense.IsActive);
    }

    [Fact]
    public void Activate_AlreadyActive_Throws()
    {
        var expense = Expense.Create(NewUser(), "Sub", Usd(5m), ExpenseCategory.Other, DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => expense.Activate());
    }

    [Fact]
    public void TryDeactivate_WhenActive_ReturnsTrueAndDeactivates()
    {
        var expense = Expense.Create(NewUser(), "Sub", Usd(5m), ExpenseCategory.Other, DateTime.UtcNow);
        var result = expense.TryDeactivate();
        Assert.True(result);
        Assert.False(expense.IsActive);
    }

    [Fact]
    public void TryDeactivate_WhenAlreadyInactive_ReturnsFalse()
    {
        var expense = Expense.Create(NewUser(), "Sub", Usd(5m), ExpenseCategory.Other, DateTime.UtcNow);
        expense.Deactivate();
        var result = expense.TryDeactivate();
        Assert.False(result);
    }
}
