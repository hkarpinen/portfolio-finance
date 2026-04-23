using Bills.Domain.ValueObjects;

namespace Bills.Application.Contracts;

/// <summary>
/// A bill split enriched with the parent bill's due date, title, household name, and recurrence info.
/// RecurrenceFrequency/StartDate/EndDate are non-null when the parent bill is recurring — used by
/// the contribution builder to project the split forward across future months.
/// </summary>
public sealed record SplitWithBillDetail(
    Guid SplitId,
    Guid BillId,
    Guid HouseholdId,
    string HouseholdName,
    string BillTitle,
    string BillCategory,
    decimal Amount,
    string Currency,
    DateTime DueDate,
    bool IsClaimed,
    DateTime? ClaimedAt,
    Guid? ClaimedBy,
    RecurrenceFrequency? RecurrenceFrequency,
    DateTime? RecurrenceStartDate,
    DateTime? RecurrenceEndDate);

/// <summary>A single split the user is responsible for, shown within a contribution period.</summary>
public sealed record ContributionItem(
    Guid SplitId,
    Guid BillId,
    Guid HouseholdId,
    string HouseholdName,
    string BillTitle,
    string BillCategory,
    decimal Amount,
    string Currency,
    DateTime DueDate,
    bool IsClaimed,
    DateTime? ClaimedAt);

/// <summary>
/// A rolled-up summary of a user's financial obligations for a specific calendar month,
/// alongside the income projected to be available that month.
/// </summary>
public sealed record ContributionPeriodSummary(
    /// <summary>Human-readable label, e.g. "April 2026".</summary>
    string PeriodLabel,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    /// <summary>Sum of all split amounts due this month (claimed + unclaimed).</summary>
    decimal TotalDue,
    /// <summary>Sum of splits already marked as paid/claimed.</summary>
    decimal TotalPaid,
    /// <summary>Income projected to arrive this month, respecting each source's frequency.</summary>
    decimal ProjectedIncome,
    /// <summary>ProjectedIncome minus TotalDue. Negative = over-committed.</summary>
    decimal NetAfterContributions,
    IReadOnlyCollection<ContributionItem> Contributions);
