namespace Finance.Application.Dtos;

public enum CoverageStatusKind
{
    FullyCovered,
    AtRisk,
    Overcommitted
}

public sealed record DashboardDto(
    Guid GroupId,
    decimal TotalGrossIncome,
    decimal TotalNetIncome,
    decimal TotalExpenses,
    decimal NetBalance,
    bool IsOvercommitted,
    decimal CoverageRatio,
    bool IsFullyCovered,
    CoverageStatusKind CoverageStatus,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal? AvailableBalance = null,
    DateTime? BalanceAsOf = null);

public sealed record LinkedAccountBalanceDto(
    Guid AccountId,
    string Name,
    string? Mask,
    string Type,
    decimal? AvailableBalance,
    decimal? CurrentBalance,
    string Currency);

public sealed record AccountBalanceSummaryDto(
    decimal? TotalAvailable,
    string? Currency,
    DateTime? AsOf,
    bool HasConnectedAccounts,
    IReadOnlyList<LinkedAccountBalanceDto> Accounts);

// NetSettlement is positive when the member owes the caller, negative when the caller owes them.
public sealed record MemberBalanceDto(
    Guid UserId,
    string DisplayName,
    decimal TotalOwed,
    decimal TotalOwedToYou,
    decimal NetSettlement,
    string Currency);

public sealed record MemberBalanceListDto(IReadOnlyList<MemberBalanceDto> Items, int TotalCount);

public sealed record SettlementSummaryDto(
    DateTime SettledAt,
    decimal Amount,
    string Currency);
