using Finance.Application.Contracts;

namespace Finance.Application.Queries;

public interface IDashboardQuery
{
    Task<DashboardResponse> QueryAsync(DashboardQueryRequest request, CancellationToken cancellationToken = default);
    Task<CoverageStatusResponse> GetCoverageStatusAsync(CoverageStatusQueryRequest request, CancellationToken cancellationToken = default);
}
