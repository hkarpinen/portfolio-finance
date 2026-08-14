using Finance.Domain.Aggregates;
using Finance.Domain.Engines;
using Finance.Domain.ValueObjects;

namespace Tests;

public class ConvergenceTests
{
    private static readonly LedgerId L = LedgerId.New();
    private static readonly AccountId Groceries = AccountId.New();
    private static readonly AccountId Payable = AccountId.New();
    private static readonly AccountId Hank = AccountId.New();
    private static readonly DateTime Jan3 = new(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc);

    private static Money Usd(decimal a) => Money.Create(a, "USD");

    private static JournalEntry Accrual(decimal amount = 90m, AccountId? credit = null) =>
        JournalEntry.Movement(L, Groceries, credit ?? Payable, Usd(amount), Jan3, "Shop — incurred", "expense:1");

    [Fact]
    public void AnEntrySayingTheSameThing_IsRecognised()
    {
        Assert.True(Accrual().SaysTheSameAs(Accrual()));
    }

    [Fact]
    public void ADifferentAmount_IsNotTheSameThing()
    {
        Assert.False(Accrual(95m).SaysTheSameAs(Accrual(90m)));
    }

    // The comparators this replaced read the first DEBIT line and nothing else. An entry whose
    // credit had moved to another account was reported as in sync and left standing — the books
    // kept saying the money was owed somewhere it was not.
    [Fact]
    public void ADifferentAccountOnTheOtherLeg_IsNotTheSameThing()
    {
        var somewhereElse = Accrual(credit: Hank);

        Assert.False(Accrual().SaysTheSameAs(somewhereElse));
    }

    [Fact]
    public void ADifferentDateOrDescription_IsNotTheSameThing()
    {
        var moved = JournalEntry.Movement(L, Groceries, Payable, Usd(90m), Jan3.AddDays(1), "Shop — incurred", "expense:1");
        var retitled = JournalEntry.Movement(L, Groceries, Payable, Usd(90m), Jan3, "Corner shop — incurred", "expense:1");

        Assert.False(Accrual().SaysTheSameAs(moved));
        Assert.False(Accrual().SaysTheSameAs(retitled));
    }

    // Order is an artefact of how the caller assembled the lines, not a fact about the entry.
    [Fact]
    public void TheSameLinesInAnotherOrder_AreTheSameEntry()
    {
        var lines = Accrual().JournalLines
            .Select(l => new JournalLineDraft(l.AccountId, l.Direction, l.Amount))
            .Reverse().ToList();
        var reversedOrder = JournalEntry.Post(L, Jan3, "Shop — incurred", lines, "expense:1");

        Assert.True(Accrual().SaysTheSameAs(reversedOrder));
    }

    [Fact]
    public void BothLegsOnOneAccount_IsRefused()
    {
        // It nets to nothing and still satisfies double-entry, so nothing downstream would object.
        Assert.Throws<InvalidOperationException>(
            () => JournalEntry.Movement(L, Groceries, Groceries, Usd(90m), Jan3, "Nowhere", "s"));
    }

    [Fact]
    public void BooksThatAlreadyAgree_NeedNoWriting()
    {
        var candidate = Accrual();

        var plan = ConvergencePlan.For(candidate, [Accrual()]);

        Assert.True(plan.AlreadyThere);
        Assert.Empty(plan.Reverse);
        Assert.Null(plan.Post);
    }

    [Fact]
    public void BooksThatSayNothing_GetTheEntry()
    {
        var candidate = Accrual();

        var plan = ConvergencePlan.For(candidate, []);

        Assert.False(plan.AlreadyThere);
        Assert.Empty(plan.Reverse);
        Assert.Same(candidate, plan.Post);
    }

    [Fact]
    public void BooksThatSaySomethingElse_AreReversedAndReplaced()
    {
        var stale = Accrual(90m);
        var candidate = Accrual(120m);

        var plan = ConvergencePlan.For(candidate, [stale]);

        Assert.Equal([stale], plan.Reverse);
        Assert.Same(candidate, plan.Post);
    }

    // Two in-effect entries under one source should not happen. Reversing both and re-posting
    // repairs it rather than leaving the duplicate to double every balance it touches.
    [Fact]
    public void SeveralEntriesUnderOneSource_AreAllReversed()
    {
        var candidate = Accrual();
        var first = Accrual();
        var second = Accrual();

        var plan = ConvergencePlan.For(candidate, [first, second]);

        Assert.Equal(2, plan.Reverse.Count);
        Assert.Same(candidate, plan.Post);
    }

    [Fact]
    public void RemovingASource_ReversesWithoutReplacing()
    {
        var posted = Accrual();

        var plan = ConvergencePlan.Remove([posted]);

        Assert.Equal([posted], plan.Reverse);
        Assert.Null(plan.Post);
    }
}
