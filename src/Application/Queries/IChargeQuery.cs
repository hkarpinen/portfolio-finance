using Finance.Application.Dtos;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Queries;

public sealed record ChargeDetailParams(Guid ChargeId);

public sealed record ListChargesParams(
    Guid UserId,
    int Page = 1,
    int PageSize = 50,
    bool ActiveOnly = true);

public sealed record ListGroupChargesParams(
    Guid GroupId,
    int Page = 1,
    int PageSize = 20,
    bool ActiveOnly = true,
    Guid? CallerId = null);

public sealed record GroupChargeDetailParams(Guid ChargeId);

public sealed record ListAllocationsParams(Guid ChargeId);

public interface IChargeQuery
{
    // ── Personal expense queries ──────────────────────────────────────────────
    Task<ChargeListDto> ListByUserAsync(ListChargesParams request, CancellationToken cancellationToken = default);
    Task<ChargeResponseDto?> GetDetailAsync(ChargeDetailParams request, CancellationToken cancellationToken = default);

    // ── Group expense queries ─────────────────────────────────────────────────
    Task<GroupChargeListDto> ListByGroupAsync(ListGroupChargesParams request, CancellationToken cancellationToken = default);
    Task<ChargeResponseDto?> GetGroupDetailAsync(GroupChargeDetailParams request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<AllocationDto>> ListAllocationsAsync(ListAllocationsParams request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a group expense with enriched splits (member name + paid status) and the caller's role.
    /// </summary>
    Task<GroupChargeDetailDto?> GetGroupChargeDetailAsync(Guid expenseId, Guid callerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a single split enriched with the member's display name and current-occurrence paid status.
    /// Used after a write (upsert) so the controller can hand the caller the same shape the detail view returns.
    /// </summary>
    Task<AllocationDetailDto?> GetAllocationDetailAsync(Guid splitId, CancellationToken cancellationToken = default);

    Task<bool> ExistsForUserAsync(UserId userId, string title, decimal amount, CancellationToken cancellationToken = default);

    // ── Charge-split queries (sub-entity of Charge) ────────────────────────
    /// Returns per-month, per-member contribution summaries for all group expenses.
    /// Recurring expenses are projected forward/back across the window.
    /// </summary>
    Task<IReadOnlyCollection<GroupMonthlyContributionsDto>> ListAllocationsByGroupAsync(
        GroupId groupId, DateTime windowStart, DateTime windowEnd, CancellationToken cancellationToken = default);

    /// <summary>
    /// Per-member net balance within a group, viewed from the caller's perspective.
    /// </summary>
    Task<MemberBalanceListDto> ListMemberBalancesAsync(
        GroupId groupId, Guid callerUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Most recent settlement (period where all splits are claimed). Null when no period is fully settled.
    /// </summary>
    Task<SettlementSummaryDto?> GetLastSettlementAsync(
        GroupId groupId, CancellationToken cancellationToken = default);
}
