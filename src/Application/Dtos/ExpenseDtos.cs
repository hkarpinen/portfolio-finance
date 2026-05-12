using Finance.Application.Dtos;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Dtos;

public sealed record ExpenseDto(
    Guid ExpenseId,
    Guid UserId,
    string Title,
    string? Description,
    decimal Amount,
    string Currency,
    string Category,
    DateTime DueDate,
    string? RecurrenceFrequency,
    DateTime? RecurrenceStartDate,
    DateTime? RecurrenceEndDate,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsPaid = false,
    DateTime? CurrentOccurrenceDate = null);

public sealed record ExpenseListDto(IReadOnlyCollection<ExpenseDto> Items, int TotalCount);

public sealed record HouseholdExpenseDto(
    Guid ExpenseId,
    Guid HouseholdId,
    string Title,
    string? Description,
    decimal Amount,
    string Currency,
    ExpenseCategory Category,
    Guid CreatedBy,
    DateTime DueDate,
    RecurrenceFrequency? RecurrenceFrequency,
    DateTime? RecurrenceStartDate,
    DateTime? RecurrenceEndDate,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime CurrentOccurrenceDate = default,
    bool CallerIsPaid = false);

public sealed record HouseholdExpenseListDto(IReadOnlyCollection<HouseholdExpenseDto> Items, int TotalCount);

/// <summary>Split enriched with member display name and role.</summary>
public sealed record SplitDetailDto(
    Guid SplitId,
    Guid MembershipId,
    Guid UserId,
    string? DisplayName,
    string? AvatarUrl,
    string Role,
    decimal Amount,
    string Currency,
    bool IsClaimed);

/// <summary>Composite read model — household expense with enriched splits and caller context.</summary>
public sealed record HouseholdExpenseDetailDto(
    HouseholdExpenseDto Expense,
    IReadOnlyCollection<SplitDetailDto> Splits,
    IReadOnlyCollection<MembershipDto> Members,
    string? CurrentUserRole);

public sealed record SplitDto(
    Guid SplitId,
    Guid ExpenseId,
    Guid HouseholdId,
    Guid MembershipId,
    Guid UserId,
    decimal Amount,
    string Currency,
    bool IsClaimed,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record CallerSplitStatusDto(Guid SplitId, bool IsPaid);
