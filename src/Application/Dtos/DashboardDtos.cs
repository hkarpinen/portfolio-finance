namespace Finance.Application.Dtos;

public sealed record DashboardDto(
    Guid HouseholdId,
    decimal TotalGrossIncome,
    decimal TotalNetIncome,
    decimal TotalBills,
    decimal NetBalance,
    bool IsOvercommitted,
    CoverageStatusDto CoverageStatus,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal? AvailableBalance = null,
    DateTime? BalanceAsOf = null);

public sealed record CoverageStatusDto(
    Guid HouseholdId,
    decimal TotalGrossIncome,
    decimal TotalNetIncome,
    decimal TotalBills,
    decimal CoverageRatio,
    bool IsFullyCovered,
    string Status,
    DateTime PeriodStart,
    DateTime PeriodEnd);

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
