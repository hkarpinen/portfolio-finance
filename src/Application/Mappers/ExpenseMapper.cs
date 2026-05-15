using Finance.Application.Dtos;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Mappers;

public static class ExpenseMapper
{
    public static ExpenseDto ToResponse(Expense expense, bool isPaid = false) =>
        new(
            expense.Id.Value,
            expense.UserId.Value,
            expense.Title,
            expense.Description,
            expense.Amount.Amount,
            expense.Amount.Currency,
            expense.Category.ToString(),
            expense.DueDate,
            expense.RecurrenceSchedule?.Frequency.ToString(),
            expense.RecurrenceSchedule?.StartDate,
            expense.RecurrenceSchedule?.EndDate,
            expense.IsActive,
            expense.CreatedAt,
            expense.UpdatedAt,
            isPaid,
            expense.RecurrenceSchedule?.CurrentOccurrence(expense.DueDate) ?? expense.DueDate);

    public static HouseholdExpenseDto ToHouseholdResponse(Expense expense, bool callerIsPaid = false) =>
        new(
            expense.Id.Value,
            expense.GroupId!.Value.Value,
            expense.Title,
            expense.Description,
            expense.Amount.Amount,
            expense.Amount.Currency,
            expense.Category,
            expense.CreatedBy!.Value.Value,
            expense.DueDate,
            expense.RecurrenceSchedule?.Frequency,
            expense.RecurrenceSchedule?.StartDate,
            expense.RecurrenceSchedule?.EndDate,
            expense.IsActive,
            expense.CreatedAt,
            expense.UpdatedAt,
            expense.RecurrenceSchedule?.CurrentOccurrence(expense.DueDate) ?? expense.DueDate,
            callerIsPaid);

    public static SplitDto ToSplitResponse(ExpenseSplit split) => new(
        split.Id.Value,
        split.ExpenseId.Value,
        split.GroupId.Value,
        split.UserId.Value,
        split.Amount.Amount,
        split.Amount.Currency,
        false,
        split.CreatedAt,
        split.UpdatedAt);
}
