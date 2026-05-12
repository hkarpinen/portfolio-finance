using Finance.Domain.ValueObjects;

namespace Finance.Domain.ReadModels;

/// <summary>
/// A bill split enriched with the parent bill's due date, title, household name, and recurrence info.
/// Used by <see cref="Finance.Domain.Engines.UserBudgetCalculator"/> and query projections.
/// RecurrenceFrequency/StartDate/EndDate are non-null when the parent bill is recurring — used by
/// the contribution builder to project the split forward across future months.
/// </summary>
public sealed record SplitWithSharedExpenseDetail(
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
