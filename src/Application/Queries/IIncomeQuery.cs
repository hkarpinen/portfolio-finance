using Bills.Application.Contracts;

namespace Bills.Application.Queries;

public interface IIncomeQuery
{
    Task<IncomeListResponse> ListAsync(ListIncomeRequest request, CancellationToken cancellationToken = default);
    Task<IncomeListResponse> ListByUserAsync(ListUserIncomeRequest request, CancellationToken cancellationToken = default);
    Task<IncomeResponse?> GetDetailAsync(IncomeDetailRequest request, CancellationToken cancellationToken = default);
}
