using Finance.Application.Contracts;
using Finance.Application.Managers.Dependencies;
using Finance.Application.Mappers;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Managers;

internal sealed class IncomeManager : IIncomeManager
{
    private readonly IIncomeSourceRepository _incomeRepository;
    private readonly IPayrollDeductionEngine _deductionEngine;

    public IncomeManager(IIncomeSourceRepository incomeRepository, IPayrollDeductionEngine deductionEngine)
    {
        _incomeRepository = incomeRepository;
        _deductionEngine = deductionEngine;
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

        if (request.InitialDeductions is { Count: > 0 })
        {
            foreach (var d in request.InitialDeductions)
            {
                income.AddDeduction(PayrollDeduction.Create(
                    Enum.Parse<DeductionType>(d.Type, ignoreCase: true),
                    d.Label,
                    Enum.Parse<DeductionCalculationMethod>(d.Method, ignoreCase: true),
                    d.Value,
                    d.IsEmployerSponsored,
                    Enum.TryParse<RecurrenceFrequency>(d.Frequency, ignoreCase: true, out var df) ? df : RecurrenceFrequency.Monthly,
                    d.IsTaxExempt));
            }
            await _incomeRepository.UpdateAsync(income, cancellationToken);
        }

        return IncomeMapper.ToResponse(income);
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
        return IncomeMapper.ToResponse(income);
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

        return IncomeMapper.ToResponse(income);
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
        return IncomeMapper.ToResponse(income);
    }

    // ── Payroll deduction operations ─────────────────────────────────────────(IncomeSource income) => IncomeMapper.ToResponse(income);

    // ── Payroll deduction operations ─────────────────────────────────────────

    public async Task<IncomeResponse?> SetTaxProfileAsync(SetTaxProfileRequest request, CancellationToken cancellationToken = default)
    {
        var income = await _incomeRepository.GetByIdAsync(IncomeId.Create(request.IncomeId), cancellationToken);
        if (income is null) return null;

        if (request.TaxProfile is null)
            income.ClearTaxProfile();
        else
            income.SetTaxProfile(TaxWithholdingProfile.Create(
                Enum.Parse<FilingStatus>(request.TaxProfile.FilingStatus, ignoreCase: true),
                request.TaxProfile.StateCode,
                request.TaxProfile.FederalAllowances,
                request.TaxProfile.StateAllowances));

        await _incomeRepository.UpdateAsync(income, cancellationToken);
        return IncomeMapper.ToResponse(income);
    }

    public async Task<IncomeResponse?> AddDeductionAsync(AddDeductionRequest request, CancellationToken cancellationToken = default)
    {
        var income = await _incomeRepository.GetByIdAsync(IncomeId.Create(request.IncomeId), cancellationToken);
        if (income is null) return null;

        var dto = request.Deduction;
        income.AddDeduction(PayrollDeduction.Create(
            Enum.Parse<DeductionType>(dto.Type, ignoreCase: true),
            dto.Label,
            Enum.Parse<DeductionCalculationMethod>(dto.Method, ignoreCase: true),
            dto.Value,
            dto.IsEmployerSponsored,
            Enum.TryParse<RecurrenceFrequency>(dto.Frequency, ignoreCase: true, out var freq) ? freq : RecurrenceFrequency.Monthly,
            dto.IsTaxExempt));

        await _incomeRepository.UpdateAsync(income, cancellationToken);
        return IncomeMapper.ToResponse(income);
    }

    public async Task<IncomeResponse?> RemoveDeductionAsync(RemoveDeductionRequest request, CancellationToken cancellationToken = default)
    {
        var income = await _incomeRepository.GetByIdAsync(IncomeId.Create(request.IncomeId), cancellationToken);
        if (income is null) return null;

        income.RemoveDeduction(
            Enum.Parse<DeductionType>(request.DeductionType, ignoreCase: true),
            request.Label);

        await _incomeRepository.UpdateAsync(income, cancellationToken);
        return IncomeMapper.ToResponse(income);
    }

}

