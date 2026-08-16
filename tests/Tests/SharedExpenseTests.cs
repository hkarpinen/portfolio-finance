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
        return Expense.Create(AccountingEntity.Group(hId),
            uId,
            "Test Expense",
            Money.Create(100m, "USD"),
            ExpenseCategory.Utilities,
            DateTime.UtcNow.Date.AddDays(1));
    }

    [Fact]
    public void Create_ShouldSetProperties()
    {
        var (hId, uId) = NewIds();
        var dueDate = DateTime.UtcNow.Date.AddDays(5);

        var expense = Expense.Create(AccountingEntity.Group(hId), uId, "Electricity", Money.Create(80m, "USD"), ExpenseCategory.Utilities, dueDate);

        Assert.Equal("Electricity", expense.Title);
        Assert.Equal(hId, expense.GroupId);
        Assert.Equal(uId, expense.EnteredBy);
        Assert.Equal(dueDate, expense.DueDate);
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
        var (hId, uId) = NewIds();

        Assert.Throws<ArgumentException>(() =>
            Expense.Create(AccountingEntity.Group(hId), uId, "", Money.Create(100m, "USD"), ExpenseCategory.Other, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var expense = CreateValidExpense();

        expense.Deactivate();

        Assert.False(expense.IsActive);
    }

    [Fact]
    public void Deactivate_AlreadyInactive_ShouldThrow()
    {
        var expense = CreateValidExpense();
        expense.Deactivate();

        Assert.Throws<InvalidOperationException>(() => expense.Deactivate());
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
    public void Update_ShouldChangeTitleAndAmount()
    {
        var expense = CreateValidExpense();
        expense.ClearDomainEvents();
        var newDueDate = DateTime.UtcNow.Date.AddDays(10);

        expense.Update("Updated Title", Money.Create(200m, "USD"), ExpenseCategory.Rent, newDueDate);

        Assert.Equal("Updated Title", expense.Title);
        Assert.Equal(200m, expense.Amount.Amount);
        Assert.Equal(ExpenseCategory.Rent, expense.Category);
    }

    [Fact]
    public void CreateGroup_WithPayer_ShouldStoreAndEmitPayer()
    {
        var (hId, uId) = NewIds();
        var payer = Guid.NewGuid();

        var expense = Expense.Create(AccountingEntity.Group(hId), uId, "Rent", Money.Create(1900m, "USD"), ExpenseCategory.Rent,
            DateTime.UtcNow.Date.AddDays(1), payerUserId: payer);

        // Assert — stored on the aggregate
        Assert.Equal(payer, expense.PayerUserId);

        // Assert — carried on the ExpenseCreated event (so read-sides/consumers can see it)
        var created = Assert.IsType<ExpenseCreated>(expense.GetDomainEvents()[0]);
        Assert.Equal(payer, created.PayerUserId);
    }

    [Fact]
    public void Update_ShouldCarryEffectivePayer_OnExpenseUpdatedEvent()
    {
        // Arrange — created with an initial payer
        var (hId, uId) = NewIds();
        var initialPayer = Guid.NewGuid();
        var expense = Expense.Create(AccountingEntity.Group(hId), uId, "Rent", Money.Create(1900m, "USD"), ExpenseCategory.Rent,
            DateTime.UtcNow.Date.AddDays(1), payerUserId: initialPayer);
        expense.ClearDomainEvents();

        // Act — change the payer via Update
        var newPayer = Guid.NewGuid();
        expense.Update(
            "Rent", Money.Create(1900m, "USD"), ExpenseCategory.Rent,
            DateTime.UtcNow.Date.AddDays(1), payerUserId: newPayer);

        // Assert — the event reflects the effective payer after the update
        var updated = Assert.IsType<ExpenseUpdated>(expense.GetDomainEvents()[0]);
        Assert.Equal(newPayer, updated.PayerUserId);
        Assert.Equal(newPayer, expense.PayerUserId);
    }

    [Fact]
    public void Update_WithNullPayer_ShouldLeaveExistingPayerUnchanged()
    {
        // Arrange — PATCH semantics: a null payer in Update means "leave as-is"
        var (hId, uId) = NewIds();
        var payer = Guid.NewGuid();
        var expense = Expense.Create(AccountingEntity.Group(hId), uId, "Rent", Money.Create(1900m, "USD"), ExpenseCategory.Rent,
            DateTime.UtcNow.Date.AddDays(1), payerUserId: payer);
        expense.ClearDomainEvents();

        // Act — update other fields, leave payer null
        expense.Update("Rent v2", Money.Create(2000m, "USD"), ExpenseCategory.Rent,
            DateTime.UtcNow.Date.AddDays(1));

        // Assert — payer preserved, and the event still carries it
        Assert.Equal(payer, expense.PayerUserId);
        var updated = Assert.IsType<ExpenseUpdated>(expense.GetDomainEvents()[0]);
        Assert.Equal(payer, updated.PayerUserId);
    }
}
