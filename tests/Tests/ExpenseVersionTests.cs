using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Tests;

/// <summary>
/// The version is what stops two people re-cutting one expense at once. Shares are their own rows, so
/// "they must not exceed the total" is read-then-write: both writers see the same total, both fit,
/// and the sum lands above it. Every write that could break that moves the version, so the second
/// one is rejected instead.
/// </summary>
public class ExpenseVersionTests
{
    private static Expense Rent() => Expense.Create(
        AccountingEntity.Group(Guid.NewGuid()), UserId.New(), "Rent",
        Money.Create(900m, "USD"), ExpenseCategory.Rent, DateTime.UtcNow.Date);

    [Fact]
    public void AShareChanging_MovesTheVersion()
    {
        var expense = Rent();
        var before = expense.Version;

        expense.RecordShareChange();

        Assert.Equal(before + 1, expense.Version);
    }

    // The total is what the shares have to fit inside, so changing it has to be serialised against
    // them too — otherwise shrinking an expense races a share being added and strands the shares above it.
    [Fact]
    public void TheTotalChanging_MovesTheVersionAsWell()
    {
        var expense = Rent();
        var before = expense.Version;

        expense.Update("Rent", Money.Create(700m, "USD"), ExpenseCategory.Rent, DateTime.UtcNow.Date);

        Assert.Equal(before + 1, expense.Version);
    }

    [Fact]
    public void EverySuccessiveChange_MovesItAgain()
    {
        var expense = Rent();

        expense.RecordShareChange();
        expense.RecordShareChange();
        expense.RecordShareChange();

        Assert.Equal(3u, expense.Version);
    }

    // Recording a share change is bookkeeping about the expense, not a fact about it — the share
    // raises its own event, and a second one here would post the same movement twice.
    [Fact]
    public void RecordingAShareChange_RaisesNoEvent()
    {
        var expense = Rent();
        expense.ClearDomainEvents();

        expense.RecordShareChange();

        Assert.Empty(expense.GetDomainEvents());
    }

    // How much an expense can be divided into is the EXPENSE's rule, asked of the expense itself
    // so no caller can supply a total that is not its own.
    [Theory]
    // Rent is 900. 600 + 300.00 was written as a separate "to the penny" case, but it is the same
    // decimal as 600 + 300 — xUnit skipped it as a duplicate id, so the boundary went untested.
    [InlineData(600, 200, true)]        // room left
    [InlineData(600, 300, true)]        // exactly the total
    [InlineData(599.99, 300.01, true)]  // exactly the total, to the penny
    [InlineData(600, 300.01, false)]    // a penny over
    [InlineData(899.99, 0.02, false)]   // a penny over, from the other side
    public void AnExpenseKnows_WhatMoreItCanBear(decimal others, decimal newShare, bool expected)
    {
        // Rent is 900.
        Assert.Equal(expected, Rent().CanBear(others, newShare));
    }

    // Shrinking below what is already divided up strands those shares above the total, which the
    // journalizing engine cannot post — it fails inside a consumer where nobody is listening.
    [Fact]
    public void ShrinkingBelowItsShares_IsRefusedWhereTheAmountChanges()
    {
        var expense = Rent();

        var error = Assert.Throws<InvalidOperationException>(() => expense.Update(
            "Rent", Money.Create(500m, "USD"), ExpenseCategory.Rent, DateTime.UtcNow.Date,
            sharesAlreadyOn: 600m));

        Assert.Contains("600", error.Message);
    }

    [Fact]
    public void ShrinkingToExactlyItsShares_IsAllowed()
    {
        Rent().Update("Rent", Money.Create(600m, "USD"), ExpenseCategory.Rent, DateTime.UtcNow.Date,
            sharesAlreadyOn: 600m);
    }

    // Fronting covers your own part ONLY when you fronted it. Under GroupCash the money came from
    // the pot, so the payer owes their share into it like everyone else — and a read that says
    // otherwise reports the payer as settled with no settlement entry behind it.
    [Fact]
    public void UnderGroupCash_TheFronterCoversNobodysShare_NotEvenTheirOwn()
    {
        var payer = Guid.NewGuid();
        var pooled = Expense.Create(
            AccountingEntity.Group(Guid.NewGuid()), UserId.New(), "Rent",
            Money.Create(900m, "USD"), ExpenseCategory.Rent, DateTime.UtcNow.Date,
            payerUserId: payer, fundingSource: FundingSource.GroupCash);

        Assert.False(pooled.CoversOwnShare(payer));
    }

    [Fact]
    public void TheFronterOfASharedExpense_CoversTheirOwnShare()
    {
        var payer = Guid.NewGuid();
        var expense = Expense.Create(
            AccountingEntity.Group(Guid.NewGuid()), UserId.New(), "Rent",
            Money.Create(900m, "USD"), ExpenseCategory.Rent, DateTime.UtcNow.Date,
            payerUserId: payer);

        Assert.True(expense.CoversOwnShare(payer));
        Assert.False(expense.CoversOwnShare(Guid.NewGuid()));
    }

    // A personal expense has no fronting and nobody to settle with.
    [Fact]
    public void AnExpenseOfYourOwn_CoversNobodysShare()
    {
        var me = UserId.New();
        var mine = Expense.CreateOwn(me, "Gym", Money.Create(40m, "USD"),
            ExpenseCategory.Other, DateTime.UtcNow.Date);

        Assert.False(mine.CoversOwnShare(me.Value));
    }
}
