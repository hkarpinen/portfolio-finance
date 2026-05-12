using Finance.Application.Dtos;

namespace Finance.Application.Queries;

public sealed record ListHouseholdsParams(
    Guid? UserId = null,
    int Page = 1,
    int PageSize = 20,
    bool ActiveOnly = true);

public sealed record HouseholdDetailParams(Guid HouseholdId);

public sealed record DashboardParams(
    Guid HouseholdId,
    DateTime PeriodStart,
    DateTime PeriodEnd);

public sealed record CoverageStatusParams(
    Guid HouseholdId,
    DateTime PeriodStart,
    DateTime PeriodEnd);

public interface IHouseholdQuery
{
    Task<HouseholdListDto> ListAsync(ListHouseholdsParams request, CancellationToken cancellationToken = default);
    Task<HouseholdDto?> GetDetailAsync(HouseholdDetailParams request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a household with its current-period bills, members, dashboard, and the
    /// requesting user's personal net balance (their own income minus all split obligations).
    /// </summary>
    Task<HouseholdDetailDto?> GetPageAsync(Guid householdId, Guid userId, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken = default);

    // ── Dashboard ─────────────────────────────────────────────────────────────
    Task<DashboardDto> QueryAsync(DashboardParams request, CancellationToken cancellationToken = default);
    Task<CoverageStatusDto> GetCoverageStatusAsync(CoverageStatusParams request, CancellationToken cancellationToken = default);

    // ── Membership ────────────────────────────────────────────────────────────
    Task<IReadOnlyCollection<MembershipDto>> ListMembersAsync(Guid householdId, CancellationToken cancellationToken = default);

    // ── User overview ─────────────────────────────────────────────────────────
    /// <summary>
    /// Returns the complete user overview: all household summaries, upcoming bills,
    /// income sources, contribution timeline, and aggregated monthly figures.
    /// </summary>
    Task<UserOverviewDto> GetUserOverviewAsync(Guid userId, DateTime now, CancellationToken cancellationToken = default);
}
