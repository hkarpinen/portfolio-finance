using Finance.Application.Dtos;
using Finance.Domain.Aggregates;

namespace Finance.Application.Mappers;

public static class ChargeMapper
{
    public static ChargeResponseDto ToResponse(Charge expense, bool isPaid = false, bool vendorPaid = false)
    {
        if (expense.GroupId.HasValue)
        {
            return new ChargeResponseDto(
                ChargeId: expense.Id.Value,
                Scope: ChargeScope.Group,
                UserId: null,
                GroupId: expense.GroupId.Value.Value,
                CreatedBy: expense.CreatedBy?.Value,
                Title: expense.Title,
                Description: expense.Description,
                Amount: expense.Amount.Amount,
                Currency: expense.Amount.Currency,
                Category: expense.Category,
                DueDate: expense.DueDate,
                RecurrenceFrequency: expense.RecurrenceSchedule?.Frequency,
                RecurrenceStartDate: expense.RecurrenceSchedule?.StartDate,
                RecurrenceEndDate: expense.RecurrenceSchedule?.EndDate,
                IsActive: expense.IsActive,
                CreatedAt: expense.CreatedAt,
                UpdatedAt: expense.UpdatedAt,
                IsPaid: isPaid,
                CurrentOccurrenceDate: expense.RecurrenceSchedule?.CurrentOccurrence(expense.DueDate) ?? expense.DueDate,
                PayerUserId: expense.PayerUserId,
                FundingSource: expense.FundingSource,
                VendorPaid: vendorPaid);
        }

        return new ChargeResponseDto(
            ChargeId: expense.Id.Value,
            Scope: ChargeScope.Personal,
            UserId: expense.UserId.Value,
            GroupId: null,
            CreatedBy: null,
            Title: expense.Title,
            Description: expense.Description,
            Amount: expense.Amount.Amount,
            Currency: expense.Amount.Currency,
            Category: expense.Category,
            DueDate: expense.DueDate,
            RecurrenceFrequency: expense.RecurrenceSchedule?.Frequency,
            RecurrenceStartDate: expense.RecurrenceSchedule?.StartDate,
            RecurrenceEndDate: expense.RecurrenceSchedule?.EndDate,
            IsActive: expense.IsActive,
            CreatedAt: expense.CreatedAt,
            UpdatedAt: expense.UpdatedAt,
            IsPaid: isPaid,
            CurrentOccurrenceDate: expense.RecurrenceSchedule?.CurrentOccurrence(expense.DueDate) ?? expense.DueDate);
    }

    public static AllocationDto ToAllocationResponse(Allocation split) => new(
        split.Id.Value,
        split.ChargeId.Value,
        split.GroupId.Value,
        split.UserId.Value,
        split.Amount.Amount,
        split.Amount.Currency,
        false,
        split.CreatedAt,
        split.UpdatedAt);
}
