using Finance.Application.Commands;
using Finance.Application.Dtos;

namespace Finance.Application.Managers;

public interface IIncomeManager
{
    Task<IncomeDto> CreateAsync(CreateIncomeCommand request, CancellationToken cancellationToken = default);
    Task<IncomeDto?> UpdateAsync(UpdateIncomeCommand request, CancellationToken cancellationToken = default);
    Task<IncomeDto?> DeleteAsync(DeleteIncomeCommand request, CancellationToken cancellationToken = default);
    Task<IncomeDto?> DeactivateAsync(DeactivateIncomeCommand request, CancellationToken cancellationToken = default);

    // ── Payroll deductions ───────────────────────────────────────────────────

    Task<IncomeDto?> SetTaxProfileAsync(SetTaxProfileCommand request, CancellationToken cancellationToken = default);
    Task<IncomeDto?> AddDeductionAsync(AddDeductionCommand request, CancellationToken cancellationToken = default);
    Task<IncomeDto?> RemoveDeductionAsync(RemoveDeductionCommand request, CancellationToken cancellationToken = default);
}
