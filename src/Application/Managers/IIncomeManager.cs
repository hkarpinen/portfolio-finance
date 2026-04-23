using Bills.Application.Contracts;

namespace Bills.Application.Managers;

public interface IIncomeManager
{
    Task<IncomeResponse> CreateAsync(CreateIncomeRequest request, CancellationToken cancellationToken = default);
    Task<IncomeResponse?> UpdateAsync(UpdateIncomeRequest request, CancellationToken cancellationToken = default);
    Task<IncomeResponse?> DeleteAsync(DeleteIncomeRequest request, CancellationToken cancellationToken = default);
    Task<IncomeResponse?> DeactivateAsync(DeactivateIncomeRequest request, CancellationToken cancellationToken = default);
}
