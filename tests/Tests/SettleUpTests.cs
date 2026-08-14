using Finance.Application.Managers;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Tests;

/// <summary>
/// Two rules that lived only in the controller, so anything reaching the manager from a consumer
/// or a test could get past them.
/// </summary>
public class SettleUpTests
{
    private static BookkeepingManager Manager() =>
        new(new BookkeepingManagerTests.FakeLedgerRepository(), null!, null!);

    // Both legs would land on ONE member account. It nets to nothing, balances perfectly, and
    // records a payment that never happened.
    [Fact]
    public async Task SettlingUpWithYourself_IsRefused()
    {
        var me = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Manager().RecordMemberTransferAsync(Guid.NewGuid(), me, me, 30m, "USD", "settleup:1"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public async Task ASettleUpOfNothingOrLess_IsRefused(decimal amount)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            Manager().RecordMemberTransferAsync(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), amount, "USD", "settleup:1"));
    }

    [Fact]
    public async Task ARealSettleUp_MovesBetweenTheTwoMembers()
    {
        var repo = new BookkeepingManagerTests.FakeLedgerRepository();
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();

        await new BookkeepingManager(repo, null!, null!)
            .RecordMemberTransferAsync(Guid.NewGuid(), from, to, 30m, "USD", "settleup:1");

        var entry = Assert.Single(repo.JournalEntries);
        Assert.Equal(2, entry.JournalLines.Count);
        Assert.Equal(from, entry.SourceMemberId);
    }
}
