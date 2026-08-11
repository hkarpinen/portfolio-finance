using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Tests;

/// <summary>
/// The schedule says which charges should exist; a charge is one of them, frozen. Everything
/// here is really one claim: a month already recorded does not move.
/// </summary>
public class ChargeScheduleTests
{
    private static readonly UserId User = UserId.Create(Guid.NewGuid());
    private static readonly GroupId Group = GroupId.Create(Guid.NewGuid());
    private static readonly DateTime Jan3 = new(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc);

    private static ChargeSchedule Rent(decimal amount = 1000m, RecurrenceFrequency freq = RecurrenceFrequency.Monthly) =>
        ChargeSchedule.Create(
            User, Group, "Rent", Money.Create(amount, "USD"), ChargeCategory.Rent,
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
    public void GenerateFrom_CopiesTheScheduleOntoTheCharge()
    {
        var schedule = Rent();

        var charge = Charge.GenerateFrom(schedule, Jan3);

        Assert.Equal(schedule.Id, charge.ScheduleId);
        Assert.Equal(Jan3, charge.OccurrenceDate);
        Assert.Equal(1000m, charge.Amount.Amount);
        Assert.Equal(schedule.GroupId, charge.GroupId);
        // The repetition stays on the schedule; a charge with its own copy would be a second
        // opinion about when it recurs.
        Assert.Null(charge.RecurrenceSchedule);
    }

    [Fact]
    public void GenerateFrom_Refuses_ADateTheScheduleNeverPlacedACharge()
    {
        var schedule = Rent();

        Assert.Throws<InvalidOperationException>(
            () => Charge.GenerateFrom(schedule, Jan3.AddDays(5)));
    }

    [Fact]
    public void AmendingTheSchedule_DoesNotMoveAChargeAlreadyGenerated()
    {
        // The whole reason this split exists. Rent goes up in March; January stays at 1,000.
        var schedule = Rent();
        var january = Charge.GenerateFrom(schedule, Jan3);

        schedule.Amend("Rent", Money.Create(1100m, "USD"), ChargeCategory.Rent, null);
        var march = Charge.GenerateFrom(schedule, new DateTime(2026, 3, 3, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(1000m, january.Amount.Amount);
        Assert.Equal(1100m, march.Amount.Amount);
    }

    [Fact]
    public void UpdatingACharge_DoesNotMoveWhichPeriodItBelongsTo()
    {
        var charge = Charge.GenerateFrom(Rent(), Jan3);

        charge.Update("Rent", Money.Create(1100m, "USD"), ChargeCategory.Rent,
            dueDate: Jan3.AddMonths(1), recurrenceSchedule: null, description: null);

        // The due date may be corrected; the occurrence is which month it reports in.
        Assert.Equal(Jan3, charge.OccurrenceDate);
    }

    [Fact]
    public void ADirectlyEnteredCharge_HasNoScheduleAndOwnsItsDate()
    {
        var charge = Charge.Create(
            User, "Coffee", Money.Create(4.50m, "USD"), ChargeCategory.Other, Jan3);

        Assert.Null(charge.ScheduleId);
        Assert.Equal(Jan3, charge.OccurrenceDate);
    }
}
