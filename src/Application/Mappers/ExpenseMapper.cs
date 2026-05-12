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
            OccurrenceDateComputer.ComputeCurrent(expense.DueDate, expense.RecurrenceSchedule));

    public static HouseholdExpenseDto ToHouseholdResponse(Expense expense, bool callerIsPaid = false) =>
        new(
            expense.Id.Value,
            expense.HouseholdId!.Value.Value,
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
            OccurrenceDateComputer.ComputeCurrent(expense.DueDate, expense.RecurrenceSchedule),
            callerIsPaid);

    public static SplitDto ToSplitResponse(ExpenseSplit split) => new(
        split.Id.Value,
        split.ExpenseId.Value,
        split.HouseholdId.Value,
        split.MembershipId.Value,
        split.UserId.Value,
        split.Amount.Amount,
        split.Amount.Currency,
        false,
        split.CreatedAt,
        split.UpdatedAt);
}
