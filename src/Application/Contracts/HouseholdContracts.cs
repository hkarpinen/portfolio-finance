namespace Bills.Application.Contracts;

public sealed record CreateHouseholdRequest(
    string Name,
    Guid OwnerId,
    string CurrencyCode = "USD",
    string? Description = null);

public sealed record UpdateHouseholdRequest(
    Guid HouseholdId,
    string Name,
    string? Description = null);

public sealed record TransferHouseholdOwnershipRequest(
    Guid HouseholdId,
    Guid NewOwnerId,
    Guid RequestingUserId);

public sealed record DeleteHouseholdRequest(
    Guid HouseholdId,
    Guid RequestingUserId);

public sealed record ListHouseholdsRequest(
    Guid? UserId = null,
    int Page = 1,
    int PageSize = 20,
    bool ActiveOnly = true);

public sealed record HouseholdDetailRequest(Guid HouseholdId);

public sealed record HouseholdResponse(
    Guid HouseholdId,
    string Name,
    string? Description,
    Guid OwnerId,
    string CurrencyCode,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record HouseholdListResponse(IReadOnlyCollection<HouseholdResponse> Items, int TotalCount);

// Composite page response — household + members + bills + dashboard in one call
public sealed record HouseholdPageResponse(
    HouseholdResponse Household,
    IReadOnlyCollection<MembershipResponse> Members,
    IReadOnlyCollection<BillResponse> Bills,
    DashboardResponse Dashboard);

// Per-household summary item for the overview/list page
public sealed record HouseholdSummaryItem(
    Guid HouseholdId,
    string Name,
    string? Description,
    string CurrencyCode,
    Guid OwnerId,
    int MemberCount,
    decimal TotalBills,
    decimal TotalIncome,
    decimal NetBalance,
    bool IsOvercommitted);

// Upcoming bill item for the overview page
public sealed record UpcomingBillItem(
    Guid BillId,
    Guid HouseholdId,
    string HouseholdName,
    string Title,
    decimal Amount,
    string Currency,
    DateTime DueDate);

// Full overview response — collapses N+1 household+dashboard+bills+income calls
public sealed record UserBillsOverviewResponse(
    IReadOnlyCollection<HouseholdSummaryItem> Households,
    IReadOnlyCollection<UpcomingBillItem> UpcomingBills,
    decimal TotalMonthlyIncome,
    /// <summary>Per-month contribution view: splits due + income available, for 12 months.</summary>
    IReadOnlyCollection<ContributionPeriodSummary> ContributionsByMonth,
    /// <summary>All active income sources for the user, for display and editing.</summary>
    IReadOnlyCollection<IncomeResponse> IncomeSources);
