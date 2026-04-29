using Finance.Application.Contracts;
using Finance.Application.Managers.Dependencies;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Managers;

internal sealed class BillWorkflowManager : IBillWorkflowManager
{
    private readonly IBillRepository _billRepository;
    private readonly IBillSplitRepository _splitRepository;
    private readonly IHouseholdMembershipRepository _membershipRepository;

    public BillWorkflowManager(
        IBillRepository billRepository,
        IBillSplitRepository splitRepository,
        IHouseholdMembershipRepository membershipRepository)
    {
        _billRepository = billRepository;
        _splitRepository = splitRepository;
        _membershipRepository = membershipRepository;
    }

    public async Task<BillResponse> CreateAsync(CreateBillRequest request, CancellationToken cancellationToken = default)
    {
        var bill = Bill.Create(
            HouseholdId.Create(request.HouseholdId),
            request.Title,
            Money.Create(request.Amount, request.Currency),
            request.Category,
            UserId.Create(request.CreatedBy),
            request.DueDate,
            BuildRecurrence(request.RecurrenceFrequency, request.RecurrenceStartDate, request.RecurrenceEndDate),
            request.Description);

        await _billRepository.AddAsync(bill, cancellationToken);
        return Map(bill);
    }

    public async Task<BillResponse?> UpdateAsync(UpdateBillRequest request, CancellationToken cancellationToken = default)
    {
        var bill = await _billRepository.GetByIdAsync(BillId.Create(request.BillId), cancellationToken);
        if (bill is null)
        {
            return null;
        }

        bill.Update(
            request.Title,
            Money.Create(request.Amount, request.Currency),
            request.Category,
            request.DueDate,
            BuildRecurrence(request.RecurrenceFrequency, request.RecurrenceStartDate, request.RecurrenceEndDate),
            request.Description);

        await _billRepository.UpdateAsync(bill, cancellationToken);
        return Map(bill);
    }

    public async Task<BillResponse?> DeactivateAsync(DeactivateBillRequest request, CancellationToken cancellationToken = default)
    {
        var bill = await _billRepository.GetByIdAsync(BillId.Create(request.BillId), cancellationToken);
        if (bill is null)
        {
            return null;
        }

        bill.Deactivate();
        await _billRepository.UpdateAsync(bill, cancellationToken);
        return Map(bill);
    }

    public async Task<SplitResponse> UpsertSplitAsync(UpsertSplitRequest request, CancellationToken cancellationToken = default)
    {
        var money = Money.Create(request.Amount, request.Currency);

        if (request.SplitId.HasValue)
        {
            var existing = await _splitRepository.GetByIdAsync(SplitId.Create(request.SplitId.Value), cancellationToken);
            if (existing is not null)
            {
                existing.Update(money);
                await _splitRepository.UpdateAsync(existing, cancellationToken);
                return MapSplit(existing);
            }
        }

        var duplicate = await _splitRepository.GetByBillAndMembershipAsync(
            BillId.Create(request.BillId),
            MembershipId.Create(request.MembershipId),
            cancellationToken);

        if (duplicate is not null)
            throw new InvalidOperationException("A split for this user already exists on this bill.");

        var split = BillSplit.Create(
            BillId.Create(request.BillId),
            HouseholdId.Create(request.HouseholdId),
            MembershipId.Create(request.MembershipId),
            UserId.Create(request.UserId),
            money);

        await _splitRepository.AddAsync(split, cancellationToken);
        return MapSplit(split);
    }

    public async Task<SplitResponse?> RemoveSplitAsync(RemoveSplitRequest request, CancellationToken cancellationToken = default)
    {
        var split = await _splitRepository.GetByIdAsync(SplitId.Create(request.SplitId), cancellationToken);
        if (split is null)
        {
            return null;
        }

        split.Remove();
        await _splitRepository.RemoveAsync(split, cancellationToken);
        return MapSplit(split);
    }

    private static BillResponse Map(Bill bill)
        => new(
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

    private static SplitResponse MapSplit(BillSplit split)
        => new(
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

    private static RecurrenceSchedule? BuildRecurrence(
        RecurrenceFrequency? frequency,
        DateTime? startDate,
        DateTime? endDate)
    {
        if (!frequency.HasValue)
        {
            return null;
        }

        var start = startDate ?? DateTime.UtcNow.Date;
        return RecurrenceSchedule.Create(frequency.Value, start, endDate);
    }
}
