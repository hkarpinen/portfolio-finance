using Finance.Application.Dtos;
using Finance.Domain.Aggregates;

namespace Finance.Application.Mappers;

public static class ExpenseMapper
{
    public static ExpenseResponseDto ToResponse(Expense expense, bool isPaid = false, bool vendorPaid = false) =>
        new(
            ExpenseId: expense.Id.Value,
            Scope: expense.Owner.IsGroup ? ExpenseScope.Group : ExpenseScope.Personal,
            OwnerId: expense.Owner.Id,
            EnteredBy: expense.EnteredBy.Value,
            Title: expense.Title,
            Description: expense.Description,
            Amount: expense.Amount.Amount,
            Currency: expense.Amount.Currency,
            Category: expense.Category,
            DueDate: expense.DueDate,
            IsActive: expense.IsActive,
            CreatedAt: expense.CreatedAt,
            UpdatedAt: expense.UpdatedAt,
            IsPaid: isPaid,
            CurrentOccurrenceDate: expense.OccurrenceDate,
            RecurringExpenseId: expense.RecurringExpenseId?.Value,
            // Only a shared bill has a payer or a funding side; on somebody's own there is nobody
            // to be paid back and nothing to pool.
            PayerUserId: expense.Owner.IsGroup ? expense.PayerUserId : null,
            FundingSource: expense.Owner.IsGroup ? expense.FundingSource : null,
            VendorPaid: expense.Owner.IsGroup && vendorPaid);

    public static ShareDto ToShareResponse(Share split) => new(
        split.Id.Value,
        split.ExpenseId.Value,
        split.UserId.Value,
        split.Amount.Amount,
        split.Amount.Currency,
        false,
        split.CreatedAt,
        split.UpdatedAt);
}
