using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Tests;

/// <summary>
/// Settling up is a document, not a call into the bookkeeper from an HTTP handler — which is what
/// gives these two rules somewhere to live where nothing can arrive past them.
/// </summary>
public class SettleUpTests
{
    private static readonly GroupId Group = GroupId.Create(Guid.NewGuid());
    private static Money Usd(decimal a) => Money.Create(a, "USD");

    // Both legs would land on ONE member account: it nets to nothing, satisfies double-entry, and
    // records a payment that never happened.
    [Fact]
    public void SettlingUpWithYourself_IsRefused()
    {
        var me = UserId.New();

        Assert.Throws<InvalidOperationException>(
            () => MemberTransfer.Record(Group, me, me, Usd(30m), DateTime.UtcNow));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void ASettleUpOfNothingOrLess_IsRefused(decimal amount)
    {
        Assert.Throws<ArgumentException>(() => MemberTransfer.Record(
            Group, UserId.New(), UserId.New(), Usd(amount), DateTime.UtcNow));
    }

    [Fact]
    public void ARealSettleUp_RaisesTheFactWithBothSides()
    {
        var from = UserId.New();
        var to = UserId.New();

        var transfer = MemberTransfer.Record(Group, from, to, Usd(30m), DateTime.UtcNow);

        var recorded = Assert.IsType<Finance.Domain.Events.MemberTransferRecorded>(
            Assert.Single(transfer.GetDomainEvents()));
        Assert.Equal(from, recorded.FromUserId);
        Assert.Equal(to, recorded.ToUserId);
        Assert.Equal(30m, recorded.Amount.Amount);
    }

    // Keyed on its own id, not on who-and-when: settle-ups repeat between the same two people, and
    // anything derived from the pair would make the second payment look like a redelivery of the
    // first and swallow it.
    [Fact]
    public void TwoSettleUpsBetweenTheSamePeople_AreDifferentFacts()
    {
        var from = UserId.New();
        var to = UserId.New();

        var first = MemberTransfer.Record(Group, from, to, Usd(30m), DateTime.UtcNow);
        var second = MemberTransfer.Record(Group, from, to, Usd(30m), DateTime.UtcNow);

        Assert.NotEqual(first.LedgerSource, second.LedgerSource);
    }

    [Fact]
    public void TheDateIsTheDayItHappened_NotTheInstant()
    {
        var at = new DateTime(2026, 3, 4, 17, 42, 11, DateTimeKind.Utc);

        var transfer = MemberTransfer.Record(Group, UserId.New(), UserId.New(), Usd(30m), at);

        Assert.Equal(at.Date, transfer.OccurredOn);
        Assert.Equal(DateTimeKind.Utc, transfer.OccurredOn.Kind);
    }
}
