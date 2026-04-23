using Bills.Application.Contracts;

namespace Bills.Application.Queries;

public interface IDashboardQuery
{
    Task<DashboardResponse> QueryAsync(DashboardQueryRequest request, CancellationToken cancellationToken = default);
    Task<CoverageStatusResponse> GetCoverageStatusAsync(CoverageStatusQueryRequest request, CancellationToken cancellationToken = default);
}
