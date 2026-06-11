namespace Finance.Application.Dtos;

/// <summary>
/// A single split occurrence shown within a contribution period. GroupId + PaidAt are populated
/// for personal-period contexts (where one user sees their own splits) and may be null for
/// per-household member breakdowns (where the group is already implied by the enclosing dto).
/// </summary>
public sealed record ContributionItemDto(
    Guid AllocationId,
    Guid BillId,
    string BillTitle,
    string BillCategory,
    decimal Amount,
    string Currency,
    DateTime DueDate,
    bool IsPaid,
    Guid? GroupId = null,
    DateTime? PaidAt = null);

/// <summary>A member's total obligation for one calendar month within a specific group. Renamed from GroupMemberContributionDto.</summary>
public sealed record GroupMemberContributionDto(
    Guid UserId,
    string? DisplayName,
    decimal TotalDue,
    decimal TotalPaid,
    IReadOnlyCollection<ContributionItemDto> Contributions);

/// <summary>Per-group monthly contributions, grouped by member. Renamed from GroupMonthlyContributionsDto.</summary>
public sealed record GroupMonthlyContributionsDto(
    string PeriodLabel,
    DateTime PeriodStart,
    decimal Total,
    string Currency,
    IReadOnlyCollection<GroupMemberContributionDto> Members);

/// <summary>
/// A rolled-up summary of a user's financial obligations for a specific calendar month,
/// alongside the income projected to be available that month.
/// </summary>
public sealed record ContributionPeriodSummaryDto(
    /// <summary>Human-readable label, e.g. "April 2026".</summary>
    string PeriodLabel,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    /// <summary>Sum of all group split amounts due this month (claimed + unclaimed).</summary>
    decimal TotalDue,
    /// <summary>Sum of group splits already marked as paid.</summary>
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
    Guid ChargeId,
    string Title,
    string Category,
    decimal Amount,
    string Currency,
    DateTime DueDate,
    bool IsPaid = false);
