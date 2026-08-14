using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Tests;

public class ExpenseAggregateTests
{
    private static UserId NewUser() => UserId.Create(Guid.NewGuid());
    private static Money Usd(decimal amount) => Money.Create(amount, "USD");

    [Fact]
    public void Create_ValidPersonalExpense_SetsProperties()
    {
        var userId = NewUser();
        var due = new DateTime(2026, 6, 1);
        var expense = Expense.CreateOwn(userId, "Netflix", Usd(15.99m), ExpenseCategory.Other, due);

        Assert.Equal(userId, expense.EnteredBy);
        Assert.Equal("Netflix", expense.Title);
        Assert.Equal(15.99m, expense.Amount.Amount);
        Assert.Null(expense.GroupId);
        // Somebody's own bill: they own it and they entered it, which used to be two fields
        // holding the same person and one holding null.
        Assert.Equal(AccountingEntity.Person(userId), expense.Owner);
        Assert.True(expense.IsActive);
        Assert.Single(expense.GetDomainEvents());
    }

    [Fact]
    public void Create_EmptyTitle_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Expense.CreateOwn(NewUser(), "", Usd(10m), ExpenseCategory.Other, DateTime.UtcNow));
    }

    [Fact]
    public void Create_NegativeAmount_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            Expense.CreateOwn(NewUser(), "Bad", Usd(-1m), ExpenseCategory.Other, DateTime.UtcNow));
    }

    [Fact]
    public void CreateGroup_SetsGroupIdAndCreatedBy()
    {
        var hId = GroupId.Create(Guid.NewGuid());
        var creator = NewUser();
        var expense = Expense.Create(AccountingEntity.Household(hId), creator, "Rent", Usd(1200m), ExpenseCategory.Other, DateTime.UtcNow);

        Assert.Equal(hId, expense.GroupId);
        Assert.Equal(creator, expense.EnteredBy);
        Assert.Equal(creator, expense.EnteredBy);
        Assert.True(expense.IsActive);
        Assert.Single(expense.GetDomainEvents());
    }

    [Fact]
    public void CreateGroup_DefaultsToPayerMemberFunding()
    {
        var expense = Expense.Create(
            AccountingEntity.Household(GroupId.Create(Guid.NewGuid())), NewUser(), "Rent", Usd(1200m), ExpenseCategory.Other, DateTime.UtcNow);

        // Front-and-reimburse is the historical default — the payer fronts the bill.
        Assert.Equal(FundingSource.PayerMember, expense.FundingSource);
    }

    [Fact]
    public void CreateGroup_RecordsPooledFundingWhenRequested()
    {
        var expense = Expense.Create(
            AccountingEntity.Household(GroupId.Create(Guid.NewGuid())), NewUser(), "Rent", Usd(1200m), ExpenseCategory.Other, DateTime.UtcNow,
            fundingSource: FundingSource.GroupCash);

        // Pooled — the vendor is paid from shared Cash; every member settles reversibly.
        Assert.Equal(FundingSource.GroupCash, expense.FundingSource);
    }

    [Fact]
    public void Update_ChangesProperties()
    {
        var expense = Expense.CreateOwn(NewUser(), "Old", Usd(10m), ExpenseCategory.Other, DateTime.UtcNow);
        expense.ClearDomainEvents();

        expense.Update("New", Usd(20m), ExpenseCategory.Other, DateTime.UtcNow.AddDays(5));

        Assert.Equal("New", expense.Title);
        Assert.Equal(20m, expense.Amount.Amount);
        Assert.Single(expense.GetDomainEvents());
    }

    [Fact]
    public void Update_EmptyTitle_Throws()
    {
        var expense = Expense.CreateOwn(NewUser(), "Valid", Usd(10m), ExpenseCategory.Other, DateTime.UtcNow);
        Assert.Throws<ArgumentException>(() =>
            expense.Update("", Usd(10m), ExpenseCategory.Other, DateTime.UtcNow));
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        var expense = Expense.CreateOwn(NewUser(), "Sub", Usd(5m), ExpenseCategory.Other, DateTime.UtcNow);
        expense.Deactivate();
        Assert.False(expense.IsActive);
    }

    [Fact]
    public void Deactivate_AlreadyInactive_Throws()
    {
        var expense = Expense.CreateOwn(NewUser(), "Sub", Usd(5m), ExpenseCategory.Other, DateTime.UtcNow);
        expense.Deactivate();
        Assert.Throws<InvalidOperationException>(() => expense.Deactivate());
    }

    [Fact]
    public void Activate_SetsIsActiveTrue()
    {
        var expense = Expense.CreateOwn(NewUser(), "Sub", Usd(5m), ExpenseCategory.Other, DateTime.UtcNow);
        expense.Deactivate();
        expense.Activate();
        Assert.True(expense.IsActive);
    }

    [Fact]
    public void Activate_AlreadyActive_Throws()
    {
        var expense = Expense.CreateOwn(NewUser(), "Sub", Usd(5m), ExpenseCategory.Other, DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => expense.Activate());
    }

    [Fact]
    public void TryDeactivate_WhenActive_ReturnsTrueAndDeactivates()
    {
        var expense = Expense.CreateOwn(NewUser(), "Sub", Usd(5m), ExpenseCategory.Other, DateTime.UtcNow);
        var result = expense.TryDeactivate();
        Assert.True(result);
        Assert.False(expense.IsActive);
    }

    [Fact]
    public void TryDeactivate_WhenAlreadyInactive_ReturnsFalse()
    {
        var expense = Expense.CreateOwn(NewUser(), "Sub", Usd(5m), ExpenseCategory.Other, DateTime.UtcNow);
        expense.Deactivate();
        var result = expense.TryDeactivate();
        Assert.False(result);
    }

    [Fact]
    public void IsManagedBy_OnAGroupBill_IsWhoeverEnteredIt()
    {
        var creator = Guid.NewGuid();
        var member = Guid.NewGuid();
        var bill = Expense.Create(
            AccountingEntity.Household(GroupId.Create(Guid.NewGuid())), UserId.Create(creator), "Rent",
            Money.Create(900m, "USD"), ExpenseCategory.Rent, DateTime.UtcNow,
            null, payerUserId: creator, fundingSource: FundingSource.PayerMember);

        Assert.True(bill.IsManagedBy(creator));
        // Being in the house lets you settle your share, not re-cut the bill.
        Assert.False(bill.IsManagedBy(member));
    }

    [Fact]
    public void IsManagedBy_OnAPersonalExpense_IsItsOwner()
    {
        var owner = Guid.NewGuid();
        var expense = Expense.CreateOwn(UserId.Create(owner), "Coffee", Money.Create(4m, "USD"),
            ExpenseCategory.Other, DateTime.UtcNow);

        Assert.True(expense.IsManagedBy(owner));
        Assert.False(expense.IsManagedBy(Guid.NewGuid()));
    }
}
