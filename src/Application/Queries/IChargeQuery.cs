using Finance.Application.Dtos;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Queries;

// CallerUserId: a personal charge is readable ONLY by its owner, so the id alone is not enough to
// resolve one.
public sealed record ChargeDetailParams(Guid ChargeId, Guid CallerUserId);

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
    Guid? CallerUserId = null);

public sealed record GroupChargeDetailParams(Guid ChargeId);

public sealed record ListAllocationsParams(Guid ChargeId);

public interface IChargeQuery
{
    Task<ChargeListDto> ListByUserAsync(ListChargesParams request, CancellationToken cancellationToken = default);
    Task<ChargeResponseDto?> GetDetailAsync(ChargeDetailParams request, CancellationToken cancellationToken = default);

    Task<GroupChargeListDto> ListByGroupAsync(ListGroupChargesParams request, CancellationToken cancellationToken = default);
    Task<ChargeResponseDto?> GetGroupDetailAsync(GroupChargeDetailParams request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<AllocationDto>> ListAllocationsAsync(ListAllocationsParams request, CancellationToken cancellationToken = default);

    Task<GroupChargeDetailDto?> GetGroupChargeDetailAsync(Guid expenseId, Guid callerUserId, CancellationToken cancellationToken = default);

    Task<AllocationDetailDto?> GetAllocationDetailAsync(Guid splitId, CancellationToken cancellationToken = default);

    Task<bool> ExistsForUserAsync(UserId userId, string title, decimal amount, CancellationToken cancellationToken = default);

    // Recurring charges are projected forward and back across the window.
    Task<IReadOnlyCollection<GroupMonthlyContributionsDto>> ListAllocationsByGroupAsync(
        GroupId groupId, DateTime windowStart, DateTime windowEnd, CancellationToken cancellationToken = default);

    // Signed from the caller's perspective.
    Task<MemberBalanceListDto> ListMemberBalancesAsync(
        GroupId groupId, Guid callerUserId, CancellationToken cancellationToken = default);

    // A settlement here is a period in which every split is claimed; null when no period is fully settled.
    Task<SettlementSummaryDto?> GetLastSettlementAsync(
        GroupId groupId, CancellationToken cancellationToken = default);
}
