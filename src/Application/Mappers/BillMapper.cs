using Finance.Application.Contracts;
using Finance.Domain.Aggregates;

namespace Finance.Application.Mappers;

public static class BillMapper
{
    public static BillResponse ToResponse(Bill bill) => new(
        bill.Id.Value,
        bill.HouseholdId.Value,
        bill.Title,
        bill.Description,
        bill.Amount.Amount,
        bill.Amount.Currency,
        bill.Category,
        bill.CreatedBy.Value,
        bill.DueDate,
        bill.RecurrenceSchedule?.Frequency,
        bill.RecurrenceSchedule?.StartDate,
        bill.RecurrenceSchedule?.EndDate,
        bill.IsActive,
        bill.CreatedAt,
        bill.UpdatedAt);

    public static SplitResponse ToSplitResponse(BillSplit split) => new(
        split.Id.Value,
        split.BillId.Value,
        split.HouseholdId.Value,
        split.MembershipId.Value,
        split.UserId.Value,
        split.Amount.Amount,
        split.Amount.Currency,
        split.IsClaimed,
        split.ClaimedAt,
        split.ClaimedBy?.Value,
        split.CreatedAt,
        split.UpdatedAt);
}
