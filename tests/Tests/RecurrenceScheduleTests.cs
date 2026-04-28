using Bills.Domain.ValueObjects;

namespace Tests;

public class RecurrenceScheduleTests
{
    [Fact]
    public void Create_ShouldSetProperties()
    {
        // Arrange
        var start = new DateTime(2024, 1, 1);

        // Act
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, start);

        // Assert
        Assert.Equal(RecurrenceFrequency.Monthly, schedule.Frequency);
        Assert.Equal(start, schedule.StartDate);
        Assert.Null(schedule.EndDate);
    }

    [Fact]
    public void Create_EndDateBeforeStartDate_ShouldThrow()
    {
        // Arrange
        var start = new DateTime(2024, 6, 1);
        var end = new DateTime(2024, 1, 1);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, start, end));
    }

    [Fact]
    public void GetOccurrencesInRange_Monthly_ShouldReturnCorrectCount()
    {
        // Arrange
        var start = new DateTime(2024, 1, 1);
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, start);
        var rangeStart = new DateTime(2024, 1, 1);
        var rangeEnd = new DateTime(2024, 7, 1);

        // Act
        var occurrences = schedule.GetOccurrencesInRange(rangeStart, rangeEnd);

        // Assert
        Assert.Equal(6, occurrences.Count); // Jan, Feb, Mar, Apr, May, Jun
    }

    [Fact]
    public void GetOccurrencesInRange_Weekly_ShouldReturnCorrectCount()
    {
        // Arrange
        var start = new DateTime(2024, 1, 1); // Monday
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Weekly, start);
        var rangeStart = new DateTime(2024, 1, 1);
        var rangeEnd = new DateTime(2024, 1, 29); // 4 weeks

        // Act
        var occurrences = schedule.GetOccurrencesInRange(rangeStart, rangeEnd);

        // Assert
        Assert.Equal(4, occurrences.Count);
    }

    [Fact]
    public void GetOccurrencesInRange_Annually_ShouldReturnCorrectCount()
    {
        // Arrange
        var start = new DateTime(2020, 1, 1);
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Annually, start);
        var rangeStart = new DateTime(2020, 1, 1);
        var rangeEnd = new DateTime(2024, 1, 1);

        // Act
        var occurrences = schedule.GetOccurrencesInRange(rangeStart, rangeEnd);

        // Assert
        Assert.Equal(4, occurrences.Count); // 2020, 2021, 2022, 2023
    }

    [Fact]
    public void GetOccurrencesInRange_StartAfterRange_ShouldReturnEmpty()
    {
        // Arrange
        var start = new DateTime(2025, 1, 1);
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, start);
        var rangeStart = new DateTime(2024, 1, 1);
        var rangeEnd = new DateTime(2024, 12, 31);

        // Act
        var occurrences = schedule.GetOccurrencesInRange(rangeStart, rangeEnd);

        // Assert
        Assert.Empty(occurrences);
    }

    [Fact]
    public void GetAmountForPeriod_Monthly_ShouldReturnTotalAmount()
    {
        // Arrange
        var start = new DateTime(2024, 1, 1);
        var schedule = RecurrenceSchedule.Create(RecurrenceFrequency.Monthly, start);
        var amount = Money.Create(100m, "USD");
        var periodStart = new DateTime(2024, 1, 1);
        var periodEnd = new DateTime(2024, 4, 1);

        // Act
        var total = schedule.GetAmountForPeriod(amount, periodStart, periodEnd);

        // Assert
        Assert.Equal(300m, total); // Jan, Feb, Mar = 3 * 100
    }
}
