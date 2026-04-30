using Finance.Application.Contracts;
using Finance.Domain.Aggregates;

namespace Finance.Application.Mappers;

public static class PersonalBillMapper
{
    public static PersonalBillResponse ToResponse(PersonalBill bill) => new(
        bill.Id.Value,
        bill.UserId.Value,
        bill.Title,
        bill.Description,
        bill.Amount.Amount,
        bill.Amount.Currency,
        bill.Category.ToString(),
        bill.DueDate,
        bill.RecurrenceSchedule?.Frequency.ToString(),
        bill.RecurrenceSchedule?.StartDate,
        bill.RecurrenceSchedule?.EndDate,
        bill.IsActive,
        bill.CreatedAt,
        bill.UpdatedAt);
}
