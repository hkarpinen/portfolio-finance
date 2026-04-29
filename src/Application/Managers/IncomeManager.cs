using Finance.Application.Contracts;
using Finance.Application.Managers.Dependencies;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Managers;

internal sealed class IncomeManager : IIncomeManager
{
    private readonly IIncomeSourceRepository _incomeRepository;

    public IncomeManager(IIncomeSourceRepository incomeRepository)
    {
        _incomeRepository = incomeRepository;
    }

    public async Task<IncomeResponse> CreateAsync(CreateIncomeRequest request, CancellationToken cancellationToken = default)
    {
        var income = IncomeSource.Create(
            UserId.Create(request.UserId),
            Money.Create(request.Amount, request.Currency),
            request.Source,
            RecurrenceSchedule.Create(request.Frequency, request.StartDate, request.EndDate),
            request.LastPaymentDate);

        await _incomeRepository.AddAsync(income, cancellationToken);
        return Map(income);
    }

    public async Task<IncomeResponse?> UpdateAsync(UpdateIncomeRequest request, CancellationToken cancellationToken = default)
    {
        var income = await _incomeRepository.GetByIdAsync(IncomeId.Create(request.IncomeId), cancellationToken);
        if (income is null)
        {
            return null;
        }

        income.Update(
            Money.Create(request.Amount, request.Currency),
            request.Source,
            RecurrenceSchedule.Create(request.Frequency, request.StartDate, request.EndDate),
            request.LastPaymentDate);

        await _incomeRepository.UpdateAsync(income, cancellationToken);
        return Map(income);
    }

    public async Task<IncomeResponse?> DeleteAsync(DeleteIncomeRequest request, CancellationToken cancellationToken = default)
    {
        var income = await _incomeRepository.GetByIdAsync(IncomeId.Create(request.IncomeId), cancellationToken);
        if (income is null)
        {
            return null;
        }

        // Domain currently supports soft-delete semantics via deactivation.
        // TryDeactivate() is idempotent: returns false without throwing if already inactive.
        if (income.TryDeactivate())
            await _incomeRepository.UpdateAsync(income, cancellationToken);

        return Map(income);
    }

    public async Task<IncomeResponse?> DeactivateAsync(DeactivateIncomeRequest request, CancellationToken cancellationToken = default)
    {
        var income = await _incomeRepository.GetByIdAsync(IncomeId.Create(request.IncomeId), cancellationToken);
        if (income is null)
        {
            return null;
        }

        income.Deactivate();
        await _incomeRepository.UpdateAsync(income, cancellationToken);
        return Map(income);
    }

    private static IncomeResponse Map(IncomeSource income)
        => new(
            income.Id.Value,
            income.UserId.Value,
            income.Amount.Amount,
            income.Amount.Currency,
            income.Source,
            income.RecurrenceSchedule.Frequency,
            income.RecurrenceSchedule.StartDate,
            income.RecurrenceSchedule.EndDate,
            income.IsActive,
            income.LastPaymentDate,
            income.CreatedAt,
            income.UpdatedAt);
}
