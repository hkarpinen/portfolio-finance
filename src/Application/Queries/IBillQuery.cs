using Bills.Application.Contracts;

namespace Bills.Application.Queries;

public interface IBillQuery
{
    Task<BillListResponse> ListAsync(ListBillsRequest request, CancellationToken cancellationToken = default);
    Task<BillResponse?> GetDetailAsync(BillDetailRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<SplitResponse>> ListSplitsAsync(ListSplitsRequest request, CancellationToken cancellationToken = default);
}
