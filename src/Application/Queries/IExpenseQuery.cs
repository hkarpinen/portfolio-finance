using Finance.Application.Dtos;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Queries;

// CallerUserId: a personal expense is readable ONLY by its owner, so the id alone is not enough to
// resolve one.
public sealed record ExpenseDetailParams(Guid ExpenseId, Guid CallerUserId);

public sealed record ListExpensesParams(
    Guid UserId,
    int Page = 1,
    int PageSize = 50,
    bool ActiveOnly = true);

public sealed record ListGroupExpensesParams(
    Guid GroupId,
    int Page = 1,
    int PageSize = 20,
    bool ActiveOnly = true,
    Guid? CallerUserId = null);

public sealed record GroupExpenseDetailParams(Guid ExpenseId);

public sealed record ListSharesParams(Guid ExpenseId);

public interface IExpenseQuery
{
    Task<ExpenseListDto> ListByUserAsync(ListExpensesParams request, CancellationToken cancellationToken = default);
    Task<ExpenseResponseDto?> GetDetailAsync(ExpenseDetailParams request, CancellationToken cancellationToken = default);

    Task<GroupExpenseListDto> ListByGroupAsync(ListGroupExpensesParams request, CancellationToken cancellationToken = default);
    Task<ExpenseResponseDto?> GetGroupDetailAsync(GroupExpenseDetailParams request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ShareDto>> ListSharesAsync(ListSharesParams request, CancellationToken cancellationToken = default);

    Task<GroupExpenseDetailDto?> GetGroupExpenseDetailAsync(Guid expenseId, Guid callerUserId, CancellationToken cancellationToken = default);

    Task<ShareDetailDto?> GetShareDetailAsync(Guid splitId, CancellationToken cancellationToken = default);

    Task<bool> ExistsForUserAsync(UserId userId, string title, decimal amount, CancellationToken cancellationToken = default);

    // Recurring expenses are projected forward and back across the window.
    Task<IReadOnlyCollection<GroupMonthlyContributionsDto>> ListSharesByGroupAsync(
        GroupId groupId, DateTime windowStart, DateTime windowEnd, CancellationToken cancellationToken = default);

    // Signed from the caller's perspective.
    Task<MemberBalanceListDto> ListMemberBalancesAsync(
        GroupId groupId, Guid callerUserId, CancellationToken cancellationToken = default);

    // A settlement here is a period in which every split is claimed; null when no period is fully settled.
    Task<SettlementSummaryDto?> GetLastSettlementAsync(
        GroupId groupId, CancellationToken cancellationToken = default);
}
