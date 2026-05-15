using Finance.Application.Dtos;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Queries;

public sealed record ListIncomeParams(
    Guid UserId,
    int Page = 1,
    int PageSize = 20,
    bool ActiveOnly = true);

public sealed record ListUserIncomeParams(
    Guid UserId,
    int Page = 1,
    int PageSize = 20,
    bool ActiveOnly = true);

public sealed record IncomeDetailParams(Guid IncomeId);

public sealed record GetNetPayBreakdownParams(
    Guid IncomeId,
    int Year,
    int Month);

public interface IIncomeQuery
{
    Task<IncomeListDto> ListAsync(ListIncomeParams request, CancellationToken cancellationToken = default);
    Task<IncomeListDto> ListByUserAsync(ListUserIncomeParams request, CancellationToken cancellationToken = default);
    Task<IncomeDto?> GetDetailAsync(IncomeDetailParams request, CancellationToken cancellationToken = default);
    Task<NetPayBreakdownDto?> GetNetPayBreakdownAsync(GetNetPayBreakdownParams request, CancellationToken cancellationToken = default);
    Task<bool> ExistsForUserAsync(UserId userId, string source, decimal amount, CancellationToken cancellationToken = default);

    // ── Contribution / budget timeline ────────────────────────────────────────
    /// <summary>
    /// Builds the full per-month contribution summary window for a user.
    /// Each entry contains projected gross and net income, household split
    /// obligations, personal bill obligations, and per-item detail.
    /// </summary>
    Task<IReadOnlyCollection<ContributionPeriodSummaryDto>> GetContributionSummariesAsync(
        Guid userId,
        DateTime now,
        int monthCount,
        int pastMonths,
        CancellationToken cancellationToken = default);
}
