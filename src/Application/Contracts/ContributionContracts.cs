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

/// <summary>Shared core fields for every split occurrence view.</summary>
public abstract record SplitOccurrenceBase(
    Guid SplitId,
    Guid BillId,
    string BillTitle,
    string BillCategory,
    decimal Amount,
    string Currency,
    DateTime DueDate,
    bool IsClaimed);

/// <summary>A single split the user is responsible for, shown within a contribution period — includes household context.</summary>
public sealed record ContributionItem(
    Guid SplitId, Guid BillId, string BillTitle, string BillCategory,
    decimal Amount, string Currency, DateTime DueDate, bool IsClaimed,
    Guid HouseholdId, string HouseholdName, DateTime? ClaimedAt)
    : SplitOccurrenceBase(SplitId, BillId, BillTitle, BillCategory, Amount, Currency, DueDate, IsClaimed);

/// <summary>A single contribution item shown inside a household's per-member breakdown — household fields omitted.</summary>
public sealed record HouseholdContributionItem(
    Guid SplitId, Guid BillId, string BillTitle, string BillCategory,
    decimal Amount, string Currency, DateTime DueDate, bool IsClaimed)
    : SplitOccurrenceBase(SplitId, BillId, BillTitle, BillCategory, Amount, Currency, DueDate, IsClaimed);

/// <summary>A member's total obligation for one calendar month within a specific household.</summary>
public sealed record HouseholdMemberContribution(
    Guid UserId,
    string? DisplayName,
    decimal TotalDue,
    decimal TotalPaid,
    IReadOnlyCollection<HouseholdContributionItem> Contributions);

/// <summary>Per-household monthly contributions, grouped by member.</summary>
public sealed record HouseholdMonthlyContributions(
    string PeriodLabel,
    DateTime PeriodStart,
    decimal Total,
    string Currency,
    IReadOnlyCollection<HouseholdMemberContribution> Members);

/// <summary>
/// A rolled-up summary of a user's financial obligations for a specific calendar month,
/// alongside the income projected to be available that month.
/// </summary>
public sealed record ContributionPeriodSummary(
    /// <summary>Human-readable label, e.g. "April 2026".</summary>
    string PeriodLabel,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    /// <summary>Sum of all household split amounts due this month (claimed + unclaimed).</summary>
    decimal TotalDue,
    /// <summary>Sum of household splits already marked as paid/claimed.</summary>
    decimal TotalPaid,
    /// <summary>Income projected to arrive this month, respecting each source's frequency.</summary>
    decimal ProjectedIncome,
    /// <summary>ProjectedIncome minus TotalDue minus PersonalBillsDue. Negative = over-committed.</summary>
    decimal NetAfterContributions,
    IReadOnlyCollection<ContributionItem> Contributions,
    /// <summary>Sum of personal bill amounts due this month (normalised by frequency).</summary>
    decimal PersonalBillsDue,
    /// <summary>Personal bill occurrences projected for this period.</summary>
    IReadOnlyCollection<PersonalBillItem> PersonalBills);

/// <summary>A single personal bill occurrence within a contribution period.</summary>
public sealed record PersonalBillItem(
    Guid PersonalBillId,
    string Title,
    string Category,
    decimal Amount,
    string Currency,
    DateTime DueDate);
