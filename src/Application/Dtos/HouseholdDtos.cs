using Finance.Application.Dtos;

namespace Finance.Application.Dtos;

public sealed record HouseholdDto(
    Guid HouseholdId,
    string Name,
    string? Description,
    Guid OwnerId,
    string CurrencyCode,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record HouseholdListDto(IReadOnlyCollection<HouseholdDto> Items, int TotalCount);

/// <summary>Composite read model — household with its related data for a given period.</summary>
public sealed record HouseholdDetailDto(
    HouseholdDto Household,
    IReadOnlyCollection<MembershipDto> Members,
    IReadOnlyCollection<HouseholdExpenseDto> Bills,
    DashboardDto Dashboard);

/// <summary>Per-household summary within a user's overall view.</summary>
public sealed record HouseholdSummaryDto(
    Guid HouseholdId,
    string Name,
    string? Description,
    string CurrencyCode,
    Guid OwnerId,
    int MemberCount,
    decimal TotalBills,
    decimal TotalGrossIncome,
    decimal NetBalance,
    bool IsOvercommitted);

/// <summary>Upcoming bill occurrence within a date window.</summary>
public sealed record UpcomingBillDto(
    Guid BillId,
    Guid HouseholdId,
    string HouseholdName,
    string Title,
    decimal Amount,
    string Currency,
    DateTime DueDate);

/// <summary>Full user overview — all households, upcoming bills, income, and contribution timeline.</summary>
public sealed record UserOverviewDto(
    IReadOnlyCollection<HouseholdSummaryDto> Households,
    IReadOnlyCollection<UpcomingBillDto> UpcomingBills,
    decimal TotalMonthlyIncome,
    IReadOnlyCollection<ContributionPeriodSummaryDto> ContributionsByMonth,
    IReadOnlyCollection<IncomeDto> IncomeSources,
    decimal TotalPersonalBillsMonthly,
    decimal TotalMonthlyNetIncome = 0m);
