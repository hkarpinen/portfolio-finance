namespace Bills.Application.Contracts;

public sealed record DashboardQueryRequest(
    Guid HouseholdId,
    DateTime PeriodStart,
    DateTime PeriodEnd);

public sealed record CoverageStatusQueryRequest(
    Guid HouseholdId,
    DateTime PeriodStart,
    DateTime PeriodEnd);

public sealed record DashboardResponse(
    Guid HouseholdId,
    decimal TotalIncome,
    decimal TotalBills,
    decimal NetBalance,
    bool IsOvercommitted,
    CoverageStatusResponse CoverageStatus,
    DateTime PeriodStart,
    DateTime PeriodEnd);

public sealed record CoverageStatusResponse(
    Guid HouseholdId,
    decimal TotalIncome,
    decimal TotalBills,
    decimal CoverageRatio,
    bool IsFullyCovered,
    string Status,
    DateTime PeriodStart,
    DateTime PeriodEnd);
