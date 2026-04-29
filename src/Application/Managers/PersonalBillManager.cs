using Finance.Application.Contracts;
using Finance.Application.Managers.Dependencies;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Managers;

internal sealed class PersonalBillManager : IPersonalBillManager
{
    private readonly IPersonalBillRepository _repository;

    public PersonalBillManager(IPersonalBillRepository repository)
    {
        _repository = repository;
    }

    public async Task<PersonalBillResponse> CreateAsync(CreatePersonalBillRequest request, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<BillCategory>(request.Category, ignoreCase: true, out var category))
            category = BillCategory.Other;

        var bill = PersonalBill.Create(
            UserId.Create(request.UserId),
            request.Title,
            Money.Create(request.Amount, request.Currency),
            category,
            request.DueDate,
            ParseSchedule(request.RecurrenceFrequency, request.RecurrenceStartDate ?? request.DueDate, request.RecurrenceEndDate),
            request.Description);

        await _repository.AddAsync(bill, cancellationToken);
        return Map(bill);
    }

    public async Task<PersonalBillResponse?> UpdateAsync(UpdatePersonalBillRequest request, CancellationToken cancellationToken = default)
    {
        var bill = await _repository.GetByIdAsync(PersonalBillId.Create(request.PersonalBillId), cancellationToken);
        if (bill is null) return null;

        if (!Enum.TryParse<BillCategory>(request.Category, ignoreCase: true, out var category))
            category = BillCategory.Other;

        bill.Update(
            request.Title,
            Money.Create(request.Amount, request.Currency),
            category,
            request.DueDate,
            ParseSchedule(request.RecurrenceFrequency, request.RecurrenceStartDate ?? request.DueDate, request.RecurrenceEndDate),
            request.Description);

        await _repository.UpdateAsync(bill, cancellationToken);
        return Map(bill);
    }

    public async Task<PersonalBillResponse?> DeleteAsync(DeletePersonalBillRequest request, CancellationToken cancellationToken = default)
    {
        var bill = await _repository.GetByIdAsync(PersonalBillId.Create(request.PersonalBillId), cancellationToken);
        if (bill is null) return null;

        if (bill.TryDeactivate())
            await _repository.UpdateAsync(bill, cancellationToken);

        return Map(bill);
    }

    private static RecurrenceSchedule? ParseSchedule(string? frequency, DateTime? startDate, DateTime? endDate)
    {
        if (string.IsNullOrWhiteSpace(frequency)
            || !Enum.TryParse<RecurrenceFrequency>(frequency, ignoreCase: true, out var freq))
            return null;

        return RecurrenceSchedule.Create(freq, startDate ?? DateTime.UtcNow, endDate);
    }

    private static PersonalBillResponse Map(PersonalBill bill) => new(
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
