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
            "Test Bill",
            Money.Create(100m, "USD"),
            ExpenseCategory.Utilities,
            DateTime.UtcNow.Date.AddDays(1));
    }

    [Fact]
    public void Create_ShouldSetProperties()
    {
        var (hId, uId) = NewIds();
        var dueDate = DateTime.UtcNow.Date.AddDays(5);

        var bill = Expense.Create(AccountingEntity.Group(hId), uId, "Electricity", Money.Create(80m, "USD"), ExpenseCategory.Utilities, dueDate);

        Assert.Equal("Electricity", bill.Title);
        Assert.Equal(hId, bill.GroupId);
        Assert.Equal(uId, bill.EnteredBy);
        Assert.Equal(dueDate, bill.DueDate);
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
        var (hId, uId) = NewIds();

        Assert.Throws<ArgumentException>(() =>
            Expense.Create(AccountingEntity.Group(hId), uId, "", Money.Create(100m, "USD"), ExpenseCategory.Other, DateTime.UtcNow.Date.AddDays(1)));
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveFalse()
    {
        var bill = CreateValidExpense();

        bill.Deactivate();

        Assert.False(bill.IsActive);
    }

    [Fact]
    public void Deactivate_AlreadyInactive_ShouldThrow()
    {
        var bill = CreateValidExpense();
        bill.Deactivate();

        Assert.Throws<InvalidOperationException>(() => bill.Deactivate());
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
    public void Update_ShouldChangeTitleAndAmount()
    {
        var bill = CreateValidExpense();
        bill.ClearDomainEvents();
        var newDueDate = DateTime.UtcNow.Date.AddDays(10);

        bill.Update("Updated Title", Money.Create(200m, "USD"), ExpenseCategory.Rent, newDueDate);

        Assert.Equal("Updated Title", bill.Title);
        Assert.Equal(200m, bill.Amount.Amount);
        Assert.Equal(ExpenseCategory.Rent, bill.Category);
    }

    [Fact]
    public void CreateGroup_WithPayer_ShouldStoreAndEmitPayer()
    {
        var (hId, uId) = NewIds();
        var payer = Guid.NewGuid();

        var bill = Expense.Create(AccountingEntity.Group(hId), uId, "Rent", Money.Create(1900m, "USD"), ExpenseCategory.Rent,
            DateTime.UtcNow.Date.AddDays(1), payerUserId: payer);

        // Assert — stored on the aggregate
        Assert.Equal(payer, bill.PayerUserId);

        // Assert — carried on the ExpenseCreated event (so read-sides/consumers can see it)
        var created = Assert.IsType<ExpenseCreated>(bill.GetDomainEvents()[0]);
        Assert.Equal(payer, created.PayerUserId);
    }

    [Fact]
    public void Update_ShouldCarryEffectivePayer_OnExpenseUpdatedEvent()
    {
        // Arrange — created with an initial payer
        var (hId, uId) = NewIds();
        var initialPayer = Guid.NewGuid();
        var bill = Expense.Create(AccountingEntity.Group(hId), uId, "Rent", Money.Create(1900m, "USD"), ExpenseCategory.Rent,
            DateTime.UtcNow.Date.AddDays(1), payerUserId: initialPayer);
        bill.ClearDomainEvents();

        // Act — change the payer via Update
        var newPayer = Guid.NewGuid();
        bill.Update(
            "Rent", Money.Create(1900m, "USD"), ExpenseCategory.Rent,
            DateTime.UtcNow.Date.AddDays(1), payerUserId: newPayer);

        // Assert — the event reflects the effective payer after the update
        var updated = Assert.IsType<ExpenseUpdated>(bill.GetDomainEvents()[0]);
        Assert.Equal(newPayer, updated.PayerUserId);
        Assert.Equal(newPayer, bill.PayerUserId);
    }

    [Fact]
    public void Update_WithNullPayer_ShouldLeaveExistingPayerUnchanged()
    {
        // Arrange — PATCH semantics: a null payer in Update means "leave as-is"
        var (hId, uId) = NewIds();
        var payer = Guid.NewGuid();
        var bill = Expense.Create(AccountingEntity.Group(hId), uId, "Rent", Money.Create(1900m, "USD"), ExpenseCategory.Rent,
            DateTime.UtcNow.Date.AddDays(1), payerUserId: payer);
        bill.ClearDomainEvents();

        // Act — update other fields, leave payer null
        bill.Update("Rent v2", Money.Create(2000m, "USD"), ExpenseCategory.Rent,
            DateTime.UtcNow.Date.AddDays(1));

        // Assert — payer preserved, and the event still carries it
        Assert.Equal(payer, bill.PayerUserId);
        var updated = Assert.IsType<ExpenseUpdated>(bill.GetDomainEvents()[0]);
        Assert.Equal(payer, updated.PayerUserId);
    }
}
