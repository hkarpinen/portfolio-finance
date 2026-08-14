using Finance.Domain.Aggregates;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Tests;

/// <summary>
/// Money arriving. There was no document for this at all, so every deposit was invisible and a
/// balance drifted from reality by each one.
/// </summary>
public class ReceiptTests
{
    private static readonly DateTime Mar4 = new(2026, 3, 4, 17, 42, 11, DateTimeKind.Utc);
    private static Money Usd(decimal a) => Money.Create(a, "USD");

    private static Receipt Salary() =>
        Receipt.Record(UserId.New(), Guid.NewGuid(), "Acme Payroll", Usd(2_400m), Mar4);

    [Fact]
    public void MoneyArriving_RecordsWhereItCameFromAndWhereItLanded()
    {
        var into = Guid.NewGuid();
        var receipt = Receipt.Record(UserId.New(), into, "Acme Payroll", Usd(2_400m), Mar4);

        Assert.Equal(into, receipt.IntoAccountId);
        Assert.Equal("Acme Payroll", receipt.Source);
        Assert.Equal(2_400m, receipt.Amount.Amount);
    }

    // The day it arrived is the period it belongs to; the time of day is not a fact about the month.
    [Fact]
    public void TheDateIsTheDayItArrived()
    {
        Assert.Equal(Mar4.Date, Salary().ReceivedOn);
        Assert.Equal(DateTimeKind.Utc, Salary().ReceivedOn.Kind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void NothingOrLessThanNothing_DidNotArrive(decimal amount)
        => Assert.Throws<ArgumentException>(
            () => Receipt.Record(UserId.New(), Guid.NewGuid(), "Acme", Usd(amount), Mar4));

    [Fact]
    public void MoneyFromNowhere_IsRefused()
        => Assert.Throws<ArgumentException>(
            () => Receipt.Record(UserId.New(), Guid.NewGuid(), "  ", Usd(10m), Mar4));

    [Fact]
    public void RecordingIt_RaisesTheFactWithEverythingTheBooksNeed()
    {
        var receipt = Salary();

        var recorded = Assert.IsType<ReceiptRecorded>(Assert.Single(receipt.GetDomainEvents()));
        Assert.Equal(receipt.IntoAccountId, recorded.IntoAccountId);
        Assert.Equal(2_400m, recorded.Amount.Amount);
    }

    // A pending deposit that never settled. Voided rather than deleted — an erased row cannot be
    // unwound, and the books have to take back what they were told.
    [Fact]
    public void OneThatDidNotArriveAfterAll_IsVoidedNotErased()
    {
        var receipt = Salary();
        receipt.ClearDomainEvents();

        receipt.Void();

        Assert.True(receipt.IsVoid);
        Assert.IsType<ReceiptVoided>(Assert.Single(receipt.GetDomainEvents()));
    }

    [Fact]
    public void VoidingTwice_SaysItOnce()
    {
        var receipt = Salary();
        receipt.Void();
        receipt.ClearDomainEvents();

        receipt.Void();

        Assert.Empty(receipt.GetDomainEvents());
    }

    // Keyed on its own id: two deposits of the same amount from the same employer on the same day
    // are two facts, and anything derived from those three would swallow the second.
    [Fact]
    public void TwoIdenticalDeposits_AreDifferentFacts()
        => Assert.NotEqual(Salary().LedgerSource, Salary().LedgerSource);
}
