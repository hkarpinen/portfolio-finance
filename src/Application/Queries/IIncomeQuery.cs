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

// CallerUserId: income is readable ONLY by its owner — the app promises it is never shown to anyone
// in the group — so the id alone must not resolve one.
public sealed record IncomeDetailParams(Guid IncomeId, Guid CallerUserId);

// Carries the caller for the same reason IncomeDetailParams does: a breakdown states gross pay,
// every deduction and take-home, so resolving one by id alone would hand over the whole payslip.
public sealed record GetNetPayBreakdownParams(
    Guid IncomeId,
    Guid CallerUserId,
    int Year,
    int Month);

public sealed record GetNetPaySummaryParams(
    Guid UserId,
    int Year,
    int Month);

public interface IIncomeQuery
{
    Task<IncomeListDto> ListAsync(ListIncomeParams request, CancellationToken cancellationToken = default);
    Task<IncomeListDto> ListByUserAsync(ListUserIncomeParams request, CancellationToken cancellationToken = default);
    Task<IncomeDto?> GetDetailAsync(IncomeDetailParams request, CancellationToken cancellationToken = default);
    Task<NetPayBreakdownDto?> GetNetPayBreakdownAsync(GetNetPayBreakdownParams request, CancellationToken cancellationToken = default);
    Task<NetPaySummaryDto> GetNetPaySummaryAsync(GetNetPaySummaryParams request, CancellationToken cancellationToken = default);
    Task<bool> ExistsForUserAsync(UserId userId, string source, decimal amount, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<ContributionPeriodSummaryDto>> GetContributionSummariesAsync(
        Guid userId,
        DateTime now,
        int monthCount,
        int pastMonths,
        CancellationToken cancellationToken = default);
}
