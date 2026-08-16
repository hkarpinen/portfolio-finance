using Finance.Domain.Aggregates;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Tests;

public class SharedShareTests
{
    private static Expense GroupExpense(decimal amount = 100m) => Expense.Create(
            AccountingEntity.Group(GroupId.Create(Guid.NewGuid())), UserId.New(), "Rent", Money.Create(amount, "USD"),
        ExpenseCategory.Rent, DateTime.UtcNow.Date);

    [Fact]
    public void Create_ShouldSetProperties()
    {
        var expense = GroupExpense();
        var userId = UserId.New();
        var amount = Money.Create(50m, "USD");

        var share = Share.Create(expense, userId, amount);

        Assert.Equal(expense.Id, share.ExpenseId);
        Assert.Equal(userId, share.UserId);
        Assert.Equal(50m, share.Amount.Amount);
    }

    [Fact]
    public void Create_ShouldRaise_ShareCreatedEvent()
    {
        var share = Share.Create(GroupExpense(), UserId.New(), Money.Create(25m, "USD"));

        Assert.Single(share.GetDomainEvents());
        Assert.IsType<ShareCreated>(share.GetDomainEvents()[0]);
    }

    // The group is the expense's. It is named on the event only because a reversal can outlive the
    // share, and it must be the group of the expense the share is actually on.
    [Fact]
    public void TheGroupOnTheEvent_IsTheExpensesGroup()
    {
        var expense = GroupExpense();
        var share = Share.Create(expense, UserId.New(), Money.Create(25m, "USD"));
        share.ClearDomainEvents();

        share.Update(expense, Money.Create(30m, "USD"));

        var updated = Assert.IsType<ShareUpdated>(share.GetDomainEvents()[0]);
        Assert.Equal(expense.GroupId, updated.GroupId);
    }

    [Fact]
    public void ASplit_RefusesAExpense_ItIsNotOn()
    {
        var share = Share.Create(GroupExpense(), UserId.New(), Money.Create(25m, "USD"));
        var somebodyElses = GroupExpense();

        Assert.Throws<InvalidOperationException>(
            () => share.Update(somebodyElses, Money.Create(30m, "USD")));
        Assert.Throws<InvalidOperationException>(() => share.Remove(somebodyElses));
    }

    [Fact]
    public void APersonalExpense_HasNoSharesToSplit()
    {
        var personal = Expense.CreateOwn(UserId.New(), "Gym", Money.Create(40m, "USD"), ExpenseCategory.Other, DateTime.UtcNow.Date);

        Assert.Throws<InvalidOperationException>(
            () => Share.Create(personal, UserId.New(), Money.Create(40m, "USD")));
    }
}
