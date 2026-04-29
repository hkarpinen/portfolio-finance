using Finance.Application.Contracts;

namespace Finance.Application.Queries;

public interface IHouseholdQuery
{
    Task<HouseholdListResponse> ListAsync(ListHouseholdsRequest request, CancellationToken cancellationToken = default);
    Task<HouseholdResponse?> GetDetailAsync(HouseholdDetailRequest request, CancellationToken cancellationToken = default);
}
