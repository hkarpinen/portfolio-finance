using Bills.Application.Contracts;

namespace Bills.Application.Queries;

public interface IPersonalBillQuery
{
    Task<PersonalBillListResponse> ListByUserAsync(ListPersonalBillsRequest request, CancellationToken cancellationToken = default);
    Task<PersonalBillResponse?> GetDetailAsync(PersonalBillDetailRequest request, CancellationToken cancellationToken = default);
}
