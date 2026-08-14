using Finance.Domain.Aggregates;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Tests;

public class IncomeSourceTests
{
    private static IncomeSource CreateIncome(decimal amount = 1000m)
    {
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, new DateTime(2024, 1, 1));
        return IncomeSource.Create(
            UserId.New(),
            Money.Create(amount, "USD"),
            "Salary",
            schedule);
    }

    [Fact]
    public void Create_ShouldSetProperties()
    {
        var userId = UserId.New();
        var amount = Money.Create(2500m, "USD");
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, new DateTime(2024, 1, 1));

        var income = IncomeSource.Create(userId, amount, "Contracting", schedule);

        Assert.Equal(userId, income.UserId);
        Assert.Equal(2500m, income.Amount.Amount);
        Assert.Equal("Contracting", income.Source);
        Assert.True(income.IsActive);
    }

    [Fact]
    public void Create_EmptySource_ShouldThrow()
    {
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, new DateTime(2024, 1, 1));
        Assert.Throws<ArgumentException>(() =>
            IncomeSource.Create(UserId.New(), Money.Create(100m, "USD"), "  ", schedule));
    }

    [Fact]
    public void Create_ShouldRaise_IncomeSourceCreatedEvent()
    {
        var income = CreateIncome();
        Assert.Single(income.GetDomainEvents());
        Assert.IsType<IncomeSourceCreated>(income.GetDomainEvents()[0]);
    }

    [Fact]
    public void Update_ShouldChangeAmountSourceAndSchedule()
    {
        var income = CreateIncome();
        income.ClearDomainEvents();
        var newSchedule = RecurrenceSchedule.Create(RecurrenceFrequency.Weekly, new DateTime(2024, 6, 1));

        income.Update(Money.Create(3000m, "USD"), "NewSource", newSchedule);

        Assert.Equal(3000m, income.Amount.Amount);
        Assert.Equal("NewSource", income.Source);
        Assert.Equal(RecurrenceFrequency.Weekly, income.RecurrenceSchedule.Frequency);
    }

    [Fact]
    public void Update_ShouldRaise_IncomeSourceUpdatedEvent()
    {
        var income = CreateIncome();
        income.ClearDomainEvents();
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, new DateTime(2024, 1, 1));

        income.Update(Money.Create(1200m, "USD"), "Salary", schedule);

        Assert.Single(income.GetDomainEvents());
        Assert.IsType<IncomeSourceUpdated>(income.GetDomainEvents()[0]);
    }

    [Fact]
    public void Update_EmptySource_ShouldThrow()
    {
        var income = CreateIncome();
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, new DateTime(2024, 1, 1));
        Assert.Throws<ArgumentException>(() =>
            income.Update(Money.Create(1m, "USD"), "", schedule));
    }

    [Fact]
    public void Deactivate_ShouldSetInactive_AndRaiseEvent()
    {
        var income = CreateIncome();
        income.ClearDomainEvents();

        income.Deactivate();

        Assert.False(income.IsActive);
        Assert.Single(income.GetDomainEvents());
        Assert.IsType<IncomeSourceDeactivated>(income.GetDomainEvents()[0]);
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_ShouldThrow()
    {
        var income = CreateIncome();
        income.Deactivate();
        Assert.Throws<InvalidOperationException>(() => income.Deactivate());
    }

    [Fact]
    public void TryDeactivate_WhenActive_ShouldReturnTrue_AndSetInactive()
    {
        var income = CreateIncome();

        var result = income.TryDeactivate();

        Assert.True(result);
        Assert.False(income.IsActive);
    }

    [Fact]
    public void TryDeactivate_WhenAlreadyInactive_ShouldReturnFalse()
    {
        var income = CreateIncome();
        income.Deactivate();

        var result = income.TryDeactivate();

        Assert.False(result);
    }

    [Fact]
    public void Activate_WhenInactive_ShouldSetActive()
    {
        var income = CreateIncome();
        income.Deactivate();

        income.Activate();

        Assert.True(income.IsActive);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ShouldThrow()
    {
        var income = CreateIncome();
        Assert.Throws<InvalidOperationException>(() => income.Activate());
    }

    // One answer to "what does this come to in a month", whichever way it is quoted. Callers were
    // reaching it two ways — the quoted amount against its quoting frequency, and per-paycheck
    // gross against the payment frequency. Both are right and they agree, which is why a third
    // route would not have been noticed when it did not.
    [Theory]
    [InlineData(60_000, RecurrenceFrequency.Annually, 5_000)]
    [InlineData(5_000, RecurrenceFrequency.Monthly, 5_000)]
    [InlineData(1_000, RecurrenceFrequency.Weekly, 4_333.33)]
    public void MonthlyGross_IsTheSameWhicheverWayTheIncomeIsQuoted(
        decimal amount, RecurrenceFrequency quotedAs, decimal expected)
    {
        var income = IncomeSource.Create(
            UserId.New(), Money.Create(amount, "USD"), "Salary",
            RecurrenceSchedule.Create(quotedAs, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

        Assert.Equal(expected, Math.Round(income.MonthlyGross(), 2));
    }

    // The two routes agreeing is the point: per-paycheck gross spread over a month's paychecks is
    // the same monthly figure as the quoted amount converted directly.
    [Fact]
    public void PerPaycheckGrossOverAMonth_AgreesWithMonthlyGross()
    {
        var income = IncomeSource.Create(
            UserId.New(), Money.Create(78_000m, "USD"), "Salary",
            RecurrenceSchedule.Create(RecurrenceFrequency.Annually, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
            paymentFrequency: RecurrenceFrequency.BiWeekly);

        var viaPaycheck = income.PerPaycheckGross()
            * RecurrenceFrequency.BiWeekly.PeriodsPerYear() / 12m;

        Assert.Equal(Math.Round(income.MonthlyGross(), 2), Math.Round(viaPaycheck, 2));
    }
}
