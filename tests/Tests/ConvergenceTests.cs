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

    private static EntryDraft Accrual(decimal amount = 90m, AccountId? credit = null) =>
        EntryDraft.Between(Groceries, credit ?? Payable, Usd(amount), Jan3, "Shop — incurred", "expense:1");

    private static JournalEntry Posted(EntryDraft d) =>
        JournalEntry.Post(L, d.Date, d.Description, d.Lines, d.Source);

    [Fact]
    public void AnEntrySayingTheSameThing_IsRecognised()
    {
        var draft = Accrual();

        Assert.True(draft.AlreadySaidBy(Posted(draft)));
    }

    [Fact]
    public void ADifferentAmount_IsNotTheSameThing()
    {
        Assert.False(Accrual(95m).AlreadySaidBy(Posted(Accrual(90m))));
    }

    // The comparators this replaced read the first DEBIT line and nothing else. An entry whose
    // credit had moved to another account was reported as in sync and left standing — the books
    // kept saying the money was owed somewhere it was not.
    [Fact]
    public void ADifferentAccountOnTheOtherLeg_IsNotTheSameThing()
    {
        var somewhereElse = Accrual(credit: Hank);

        Assert.False(Accrual().AlreadySaidBy(Posted(somewhereElse)));
    }

    [Fact]
    public void ADifferentDateOrDescription_IsNotTheSameThing()
    {
        var moved = EntryDraft.Between(Groceries, Payable, Usd(90m), Jan3.AddDays(1), "Shop — incurred", "expense:1");
        var retitled = EntryDraft.Between(Groceries, Payable, Usd(90m), Jan3, "Corner shop — incurred", "expense:1");

        Assert.False(Accrual().AlreadySaidBy(Posted(moved)));
        Assert.False(Accrual().AlreadySaidBy(Posted(retitled)));
    }

    // Order is an artefact of how the draft was assembled, not a fact about the entry.
    [Fact]
    public void TheSameLinesInAnotherOrder_AreTheSameEntry()
    {
        var draft = Accrual();
        var reversedOrder = new EntryDraft(
            draft.Source, draft.Date, draft.Description, draft.Lines.Reverse().ToList());

        Assert.True(draft.AlreadySaidBy(Posted(reversedOrder)));
    }

    [Fact]
    public void BothLegsOnOneAccount_IsRefused()
    {
        // It nets to nothing and still satisfies double-entry, so nothing downstream would object.
        Assert.Throws<InvalidOperationException>(
            () => EntryDraft.Between(Groceries, Groceries, Usd(90m), Jan3, "Nowhere", "s"));
    }

    [Fact]
    public void BooksThatAlreadyAgree_NeedNoWriting()
    {
        var draft = Accrual();

        var plan = ConvergencePlan.For(draft, [Posted(draft)]);

        Assert.True(plan.AlreadyThere);
        Assert.Empty(plan.Reverse);
        Assert.Null(plan.Post);
    }

    [Fact]
    public void BooksThatSayNothing_GetTheDraft()
    {
        var draft = Accrual();

        var plan = ConvergencePlan.For(draft, []);

        Assert.False(plan.AlreadyThere);
        Assert.Empty(plan.Reverse);
        Assert.Same(draft, plan.Post);
    }

    [Fact]
    public void BooksThatSaySomethingElse_AreReversedAndReplaced()
    {
        var stale = Posted(Accrual(90m));
        var draft = Accrual(120m);

        var plan = ConvergencePlan.For(draft, [stale]);

        Assert.Equal([stale], plan.Reverse);
        Assert.Same(draft, plan.Post);
    }

    // Two in-effect entries under one source should not happen. Reversing both and re-posting
    // repairs it rather than leaving the duplicate to double every balance it touches.
    [Fact]
    public void SeveralEntriesUnderOneSource_AreAllReversed()
    {
        var draft = Accrual();
        var first = Posted(draft);
        var second = Posted(draft);

        var plan = ConvergencePlan.For(draft, [first, second]);

        Assert.Equal(2, plan.Reverse.Count);
        Assert.Same(draft, plan.Post);
    }

    [Fact]
    public void RemovingASource_ReversesWithoutReplacing()
    {
        var posted = Posted(Accrual());

        var plan = ConvergencePlan.Remove([posted]);

        Assert.Equal([posted], plan.Reverse);
        Assert.Null(plan.Post);
    }
}
