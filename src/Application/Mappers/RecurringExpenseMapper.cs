using Finance.Application.Dtos;
using Finance.Domain.Aggregates;

namespace Finance.Application.Mappers;

public static class RecurringExpenseMapper
{
    public static RecurringExpenseDto ToResponse(RecurringExpense schedule) => new(
        RecurringExpenseId: schedule.Id.Value,
        GroupId: schedule.GroupId?.Value,
        Title: schedule.Title,
        Description: schedule.Description,
        Amount: schedule.Amount.Amount,
        Currency: schedule.Currency,
        Category: schedule.Category.ToString(),
        Frequency: schedule.Recurrence.Frequency.ToString(),
        AnchorDate: schedule.Recurrence.StartDate,
        EndDate: schedule.Recurrence.EndDate,
        PayerUserId: schedule.PayerUserId,
        FundingSource: schedule.FundingSource.ToString(),
        IsActive: schedule.IsActive);

    /// <summary>
    /// One date the schedule covers. A recorded occurrence reports what it was BILLED at; one with
    /// nothing behind it quotes the schedule as it stood on that date, which is the forecast.
    /// </summary>
    public static ScheduledOccurrenceDto ToOccurrence(RecurringExpense schedule, DateTime date, Expense? recorded) =>
        new(
            OccurrenceDate: date,
            Amount: recorded?.Amount.Amount ?? schedule.AmountOn(date).Amount,
            Currency: recorded?.Amount.Currency ?? schedule.Currency,
            ExpenseId: recorded?.Id.Value);
}
