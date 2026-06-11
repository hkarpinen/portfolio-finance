namespace Finance.Application.Dtos;

public enum CoverageStatusKind
{
    FullyCovered,
    AtRisk,
    Overcommitted
}

/// <summary>
/// Group financial snapshot — used to be DashboardDto. Coverage fields are inlined
/// (previously nested in CoverageStatusDto with 90% duplicate fields).
/// </summary>
public sealed record DashboardDto(
    Guid GroupId,
    decimal TotalGrossIncome,
    decimal TotalNetIncome,
    decimal TotalBills,
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

/// <summary>
/// Per-member balance for the "YOU OWE / YOU'RE OWED" badges on the household detail UI.
/// NetSettlement is positive when the member owes the caller, negative when the caller owes them.
/// </summary>
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
