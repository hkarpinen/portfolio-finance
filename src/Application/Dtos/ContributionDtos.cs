namespace Finance.Application.Dtos;

/// <summary>Shared core fields for every split occurrence view.</summary>
public abstract record SplitOccurrenceBaseDto(
    Guid SplitId,
    Guid BillId,
    string BillTitle,
    string BillCategory,
    decimal Amount,
    string Currency,
    DateTime DueDate,
    bool IsClaimed);

/// <summary>A single split the user is responsible for, shown within a contribution period.</summary>
public sealed record ContributionItemDto(
    Guid SplitId, Guid BillId, string BillTitle, string BillCategory,
    decimal Amount, string Currency, DateTime DueDate, bool IsClaimed,
    Guid GroupId, DateTime? ClaimedAt)
    : SplitOccurrenceBaseDto(SplitId, BillId, BillTitle, BillCategory, Amount, Currency, DueDate, IsClaimed);

/// <summary>A single contribution item shown inside a household's per-member breakdown — household fields omitted.</summary>
public sealed record HouseholdContributionItemDto(
    Guid SplitId, Guid BillId, string BillTitle, string BillCategory,
    decimal Amount, string Currency, DateTime DueDate, bool IsClaimed)
    : SplitOccurrenceBaseDto(SplitId, BillId, BillTitle, BillCategory, Amount, Currency, DueDate, IsClaimed);

/// <summary>A member's total obligation for one calendar month within a specific household.</summary>
public sealed record HouseholdMemberContributionDto(
    Guid UserId,
    string? DisplayName,
    decimal TotalDue,
    decimal TotalPaid,
    IReadOnlyCollection<HouseholdContributionItemDto> Contributions);

/// <summary>Per-household monthly contributions, grouped by member.</summary>
public sealed record HouseholdMonthlyContributionsDto(
    string PeriodLabel,
    DateTime PeriodStart,
    decimal Total,
    string Currency,
    IReadOnlyCollection<HouseholdMemberContributionDto> Members);

/// <summary>
/// A rolled-up summary of a user's financial obligations for a specific calendar month,
/// alongside the income projected to be available that month.
/// </summary>
public sealed record ContributionPeriodSummaryDto(
    /// <summary>Human-readable label, e.g. "April 2026".</summary>
    string PeriodLabel,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    /// <summary>Sum of all household split amounts due this month (claimed + unclaimed).</summary>
    decimal TotalDue,
    /// <summary>Sum of household splits already marked as paid/claimed.</summary>
    decimal TotalPaid,
    /// <summary>Gross income projected to arrive this month, respecting each source's frequency.</summary>
    decimal ProjectedIncome,
    IReadOnlyCollection<ContributionItemDto> Contributions,
    /// <summary>Sum of personal bill amounts due this month (normalised by frequency).</summary>
    decimal PersonalBillsDue,
    /// <summary>Personal bill occurrences projected for this period.</summary>
    IReadOnlyCollection<PersonalBillItemDto> PersonalBills,
    /// <summary>Net take-home income after all payroll deductions (taxes + voluntary). Equals ProjectedIncome when no deductions are configured.</summary>
    decimal ProjectedNetIncome = 0m,
    /// <summary>Sum of personal bill occurrences already marked as paid in the period.</summary>
    decimal PersonalBillsPaid = 0m,
    /// <summary>
    /// Discretionary income available for the period.
    /// Past/current months without a bank connection: ProjectedNetIncome − TotalDue − PersonalBillsDue (income-math estimate).
    /// Current month with a bank connection: sum(checking AvailableBalance) − unpaid obligations.
    /// Future months: null.
    /// </summary>
    decimal? DisposableIncome = null,
    /// <summary>How DisposableIncome was derived: "balance" | "estimate" | null.</summary>
    string? DisposableIncomeSource = null);

/// <summary>A single personal bill occurrence within a contribution period.</summary>
public sealed record PersonalBillItemDto(
    Guid ExpenseId,
    string Title,
    string Category,
    decimal Amount,
    string Currency,
    DateTime DueDate,
    bool IsPaid = false);
