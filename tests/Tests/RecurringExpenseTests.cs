using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Tests;

/// <summary>
/// The schedule says which expenses should exist; an expense is one of them, frozen. Everything
/// here is really one claim: a month already recorded does not move.
/// </summary>
public class RecurringExpenseTests
{
    private static readonly UserId User = UserId.Create(Guid.NewGuid());
    private static readonly GroupId Group = GroupId.Create(Guid.NewGuid());
    private static readonly DateTime Jan3 = new(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc);

    private static RecurringExpense Rent(decimal amount = 1000m, RecurrenceFrequency freq = RecurrenceFrequency.Monthly) =>
        RecurringExpense.Create(
            AccountingEntity.Group(Group), User, "Rent", Money.Create(amount, "USD"), ExpenseCategory.Rent,
            RecurrenceSchedule.Create(freq, Jan3));

    [Fact]
    public void OccurrencesIn_StepsFromTheAnchor_NotTheFirstOfTheMonth()
    {
        var dates = Rent().OccurrencesIn(Jan3, Jan3.AddMonths(3));

        Assert.Equal(
            [Jan3, new DateTime(2026, 2, 3, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 3, 3, 0, 0, 0, DateTimeKind.Utc)],
            dates);
    }

    [Fact]
    public void OccurrencesIn_BiWeeklyIsNotTwiceMonthly()
    {
        var dates = Rent(freq: RecurrenceFrequency.BiWeekly).OccurrencesIn(Jan3, Jan3.AddYears(1));

        // The claim is not "26 a year" — a window starting on an occurrence holds 27, because
        // 365/14 is 26.07. The claim is that some MONTHS hold three, which is what a
        // twice-monthly assumption gets wrong and what a monthly average hides.
        var perMonth = dates.GroupBy(d => new { d.Year, d.Month }).Select(g => g.Count()).ToList();

        Assert.Contains(3, perMonth);
        Assert.DoesNotContain(perMonth, c => c > 3);
        Assert.All(dates.Zip(dates.Skip(1)), pair => Assert.Equal(14, (pair.Second - pair.First).TotalDays));
    }

    [Fact]
    public void OccurrencesIn_IsEmpty_WhenDeactivated()
    {
        var schedule = Rent();
        schedule.Deactivate();

        Assert.Empty(schedule.OccurrencesIn(Jan3, Jan3.AddYears(1)));
    }

    [Fact]
    public void GenerateFrom_CopiesTheScheduleOntoTheExpense()
    {
        var schedule = Rent();

        var expense = Expense.GenerateFrom(schedule, Jan3);

        Assert.Equal(schedule.Id, expense.RecurringExpenseId);
        Assert.Equal(Jan3, expense.OccurrenceDate);
        Assert.Equal(1000m, expense.Amount.Amount);
        Assert.Equal(schedule.GroupId, expense.GroupId);
    }

    [Fact]
    public void GenerateFrom_Refuses_ADateTheScheduleNeverPlacedAExpense()
    {
        var schedule = Rent();

        Assert.Throws<InvalidOperationException>(
            () => Expense.GenerateFrom(schedule, Jan3.AddDays(5)));
    }

    [Fact]
    public void AmendingTheSchedule_DoesNotMoveAExpenseAlreadyGenerated()
    {
        // The whole reason this split exists. Rent goes up in March; January stays at 1,000.
        var schedule = Rent();
        var january = Expense.GenerateFrom(schedule, Jan3);

        schedule.Amend("Rent", Money.Create(1100m, "USD"), ExpenseCategory.Rent, null,
            effectiveFrom: new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        var march = Expense.GenerateFrom(schedule, new DateTime(2026, 3, 3, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(1000m, january.Amount.Amount);
        Assert.Equal(1100m, march.Amount.Amount);
    }

    [Fact]
    public void UpdatingAExpense_DoesNotMoveWhichPeriodItBelongsTo()
    {
        var expense = Expense.GenerateFrom(Rent(), Jan3);

        expense.Update("Rent", Money.Create(1100m, "USD"), ExpenseCategory.Rent,
            dueDate: Jan3.AddMonths(1), description: null);

        // The due date may be corrected; the occurrence is which month it reports in.
        Assert.Equal(Jan3, expense.OccurrenceDate);
    }

    [Fact]
    public void ADirectlyEnteredExpense_HasNoScheduleAndOwnsItsDate()
    {
        var expense = Expense.CreateOwn(User, "Coffee", Money.Create(4.50m, "USD"), ExpenseCategory.Other, Jan3);

        Assert.Null(expense.RecurringExpenseId);
        Assert.Equal(Jan3, expense.OccurrenceDate);
    }

    [Fact]
    public void AmountOn_UsesTheVersionInForceThen_NotTodaysFigure()
    {
        var schedule = Rent();
        schedule.Amend("Rent", Money.Create(1100m, "USD"), ExpenseCategory.Rent, null,
            effectiveFrom: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(1000m, schedule.AmountOn(new DateTime(2026, 5, 3, 0, 0, 0, DateTimeKind.Utc)).Amount);
        Assert.Equal(1100m, schedule.AmountOn(new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc)).Amount);
    }

    [Fact]
    public void GenerateFrom_BillsAMonthRecordedLateAtWhatWasAgreedThen()
    {
        // Nobody got round to recording May until August. It still bills May's rent.
        var schedule = Rent();
        schedule.Amend("Rent", Money.Create(1100m, "USD"), ExpenseCategory.Rent, null,
            effectiveFrom: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        var may = Expense.GenerateFrom(schedule, new DateTime(2026, 5, 3, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(1000m, may.Amount.Amount);
    }

    [Fact]
    public void Amend_ReplacesAVersionOnTheSameDay_RatherThanStackingTwo()
    {
        var schedule = Rent();
        var june = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        schedule.Amend("Rent", Money.Create(1100m, "USD"), ExpenseCategory.Rent, null, june);
        schedule.Amend("Rent", Money.Create(1150m, "USD"), ExpenseCategory.Rent, null, june);

        // A typo corrected must not leave two answers for one day.
        Assert.Equal(2, schedule.Amounts.Count);
        Assert.Equal(1150m, schedule.AmountOn(june).Amount);
    }

    [Fact]
    public void Amend_RefusesToChangeTheCurrencyMidAgreement()
    {
        var schedule = Rent();

        Assert.Throws<InvalidOperationException>(
            () => schedule.Amend("Rent", Money.Create(900m, "EUR"), ExpenseCategory.Rent, null));
    }
}
