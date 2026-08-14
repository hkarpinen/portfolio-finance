using Finance.Application.Commands;
using Finance.Application.Dtos;
using Finance.Application.Ports;
using Finance.Application.Repositories;
using Finance.Application.Mappers;
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

    public async Task<IncomeDto> CreateAsync(CreateIncomeCommand request, CancellationToken cancellationToken = default)
    {
        var lastPaycheckDate = request.LastPaycheckDate ?? request.StartDate;
        var income = IncomeSource.Create(
            UserId.Create(request.UserId),
            Money.Create(request.Amount, request.Currency),
            request.Source,
            RecurrenceSchedule.Create(request.QuotedAs, request.StartDate, request.EndDate),
            request.PaidEvery,
            lastPaycheckDate,
            request.Notes);

        if (request.InitialDeductions is { Count: > 0 })
        {
            foreach (var d in request.InitialDeductions)
            {
                income.AddDeduction(IncomeMapper.ToDeduction(d));
            }
        }

        await _incomeRepository.AddAsync(income, cancellationToken);
        await _incomeRepository.CommitAsync(cancellationToken);

        return IncomeMapper.ToResponse(income);
    }

    public async Task<IncomeDto?> UpdateAsync(UpdateIncomeCommand request, CancellationToken cancellationToken = default)
    {
        var income = await _incomeRepository.GetByIdAsync(IncomeId.Create(request.IncomeId), cancellationToken);
        if (income is null)
        {
            return null;
        }

        // Owner check. Null (→ 404), never 403 — a 403 would confirm the id exists.
        if (income.UserId.Value != request.CallerUserId)
        {
            return null;
        }

        var lastPaycheckDate = request.LastPaycheckDate ?? request.StartDate;
        income.Update(
            Money.Create(request.Amount, request.Currency),
            request.Source,
            RecurrenceSchedule.Create(request.QuotedAs, request.StartDate, request.EndDate),
            request.PaidEvery,
            lastPaycheckDate,
            request.Notes);

        await _incomeRepository.UpdateAsync(income, cancellationToken);
        await _incomeRepository.CommitAsync(cancellationToken);
        return IncomeMapper.ToResponse(income);
    }

    public async Task<IncomeDto?> DeleteAsync(DeleteIncomeCommand request, CancellationToken cancellationToken = default)
    {
        var income = await _incomeRepository.GetByIdAsync(IncomeId.Create(request.IncomeId), cancellationToken);
        if (income is null)
        {
            return null;
        }

        // Deletion is a soft delete. TryDeactivate() is idempotent: false, not a throw, if already inactive.
        if (income.TryDeactivate())
        {
            await _incomeRepository.UpdateAsync(income, cancellationToken);
            await _incomeRepository.CommitAsync(cancellationToken);
        }

        return IncomeMapper.ToResponse(income);
    }

    public async Task<IncomeDto?> DeactivateAsync(DeactivateIncomeCommand request, CancellationToken cancellationToken = default)
    {
        var income = await _incomeRepository.GetByIdAsync(IncomeId.Create(request.IncomeId), cancellationToken);
        if (income is null)
        {
            return null;
        }

        // Owner check. Null (→ 404), never 403.
        if (income.UserId.Value != request.CallerUserId)
        {
            return null;
        }

        income.Deactivate();
        await _incomeRepository.UpdateAsync(income, cancellationToken);
        await _incomeRepository.CommitAsync(cancellationToken);
        return IncomeMapper.ToResponse(income);
    }


    public async Task<IncomeDto?> SetTaxProfileAsync(SetTaxProfileCommand request, CancellationToken cancellationToken = default)
    {
        var income = await _incomeRepository.GetByIdAsync(IncomeId.Create(request.IncomeId), cancellationToken);
        if (income is null) return null;

        // Owner check. Null (→ 404), never 403 — a 403 would confirm the id exists.
        if (income.UserId.Value != request.CallerUserId) return null;

        if (request.TaxProfile is null)
            income.ClearTaxProfile();
        else
            income.SetTaxProfile(TaxWithholdingProfile.Create(
                request.TaxProfile.FilingStatus,
                request.TaxProfile.StateCode,
                request.TaxProfile.FederalAllowances,
                request.TaxProfile.StateAllowances));

        await _incomeRepository.UpdateAsync(income, cancellationToken);
        await _incomeRepository.CommitAsync(cancellationToken);
        return IncomeMapper.ToResponse(income);
    }

    public async Task<IncomeDto?> AddDeductionAsync(AddDeductionCommand request, CancellationToken cancellationToken = default)
    {
        var income = await _incomeRepository.GetByIdAsync(IncomeId.Create(request.IncomeId), cancellationToken);
        if (income is null) return null;

        // Owner check. Null (→ 404), never 403 — a 403 would confirm the id exists.
        if (income.UserId.Value != request.CallerUserId) return null;

        income.AddDeduction(IncomeMapper.ToDeduction(request.Deduction));

        await _incomeRepository.UpdateAsync(income, cancellationToken);
        await _incomeRepository.CommitAsync(cancellationToken);
        return IncomeMapper.ToResponse(income);
    }

    public async Task<IncomeDto?> RemoveDeductionAsync(RemoveDeductionCommand request, CancellationToken cancellationToken = default)
    {
        var income = await _incomeRepository.GetByIdAsync(IncomeId.Create(request.IncomeId), cancellationToken);
        if (income is null) return null;

        // Owner check. Null (→ 404), never 403 — a 403 would confirm the id exists.
        if (income.UserId.Value != request.CallerUserId) return null;

        income.RemoveDeduction(
            Enum.Parse<DeductionType>(request.DeductionType, ignoreCase: true),
            request.Label);

        await _incomeRepository.UpdateAsync(income, cancellationToken);
        await _incomeRepository.CommitAsync(cancellationToken);
        return IncomeMapper.ToResponse(income);
    }

}

