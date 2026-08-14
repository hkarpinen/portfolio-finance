using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Tests;

/// <summary>
/// The version is what stops two people re-cutting one bill at once. Shares are their own rows, so
/// "they must not exceed the total" is read-then-write: both writers see the same total, both fit,
/// and the sum lands above it. Every write that could break that moves the version, so the second
/// one is rejected instead.
/// </summary>
public class ExpenseVersionTests
{
    private static Expense Rent() => Expense.Create(
        AccountingEntity.Household(Guid.NewGuid()), UserId.New(), "Rent",
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
    // them too — otherwise shrinking a bill races a share being added and strands the shares above it.
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

    // The rule about how much an expense can be divided into is the EXPENSE's — it used to be a
    // static taking the total as an argument, so nothing stopped a caller handing it one that was
    // not this expense's.
    [Theory]
    [InlineData(600, 300, true)]    // room left
    [InlineData(600, 300.00, true)] // to the penny
    [InlineData(600, 300.01, false)]// a penny over
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

    // Whoever fronted a shared bill has already borne their part; their share never gets settled.
    // Read in three places from two fields before this, which is one rule able to drift twice.
    [Fact]
    public void TheFronterOfASharedBill_CoversTheirOwnShare()
    {
        var payer = Guid.NewGuid();
        var expense = Expense.Create(
            AccountingEntity.Household(Guid.NewGuid()), UserId.New(), "Rent",
            Money.Create(900m, "USD"), ExpenseCategory.Rent, DateTime.UtcNow.Date,
            payerUserId: payer);

        Assert.True(expense.CoversOwnShare(payer));
        Assert.False(expense.CoversOwnShare(Guid.NewGuid()));
    }

    // A personal expense has no fronting and nobody to reimburse.
    [Fact]
    public void AnExpenseOfYourOwn_CoversNobodysShare()
    {
        var me = UserId.New();
        var mine = Expense.CreateOwn(me, "Gym", Money.Create(40m, "USD"),
            ExpenseCategory.Other, DateTime.UtcNow.Date);

        Assert.False(mine.CoversOwnShare(me.Value));
    }
}
