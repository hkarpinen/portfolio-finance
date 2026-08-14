namespace Finance.Application.Dtos;

// GroupId and PaidAt are populated only for personal-period contexts; they are null in
// per-group member breakdowns, where the group is implied by the enclosing dto.
public sealed record ContributionItemDto(
    Guid ShareId,
    Guid BillId,
    string BillTitle,
    string BillCategory,
    decimal Amount,
    string Currency,
    DateTime DueDate,
    bool IsPaid,
    Guid? GroupId = null,
    DateTime? PaidAt = null);

public sealed record GroupMemberContributionDto(
    Guid UserId,
    string? DisplayName,
    decimal TotalDue,
    decimal TotalPaid,
    IReadOnlyCollection<ContributionItemDto> Contributions);

public sealed record GroupMonthlyContributionsDto(
    string PeriodLabel,
    DateTime PeriodStart,
    decimal Total,
    string Currency,
    IReadOnlyCollection<GroupMemberContributionDto> Members);

public sealed record ContributionPeriodSummaryDto(
    string PeriodLabel,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    // Claimed and unclaimed alike.
    decimal TotalDue,
    decimal TotalPaid,
    decimal ProjectedIncome,
    IReadOnlyCollection<ContributionItemDto> Contributions,
    // Normalised by frequency.
    decimal PersonalBillsDue,
    IReadOnlyCollection<PersonalBillItemDto> PersonalBills,
    // Equals ProjectedIncome when no deductions are configured.
    decimal ProjectedNetIncome = 0m,
    decimal PersonalBillsPaid = 0m,
    // Past/current months without a bank connection: ProjectedNetIncome − TotalDue − PersonalBillsDue.
    // Current month with a bank connection: sum(checking AvailableBalance) − unpaid obligations.
    // Future months: null.
    decimal? DisposableIncome = null,
    // "balance" | "estimate" | null.
    string? DisposableIncomeSource = null);

public sealed record PersonalBillItemDto(
    Guid ExpenseId,
    string Title,
    string Category,
    decimal Amount,
    string Currency,
    DateTime DueDate,
    bool IsPaid = false);
