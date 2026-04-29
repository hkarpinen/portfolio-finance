using Finance.Domain.ValueObjects;

namespace Finance.Application.Contracts;

public sealed record CreateBillRequest(
    Guid HouseholdId,
    string Title,
    decimal Amount,
    string Currency,
    BillCategory Category,
    Guid CreatedBy,
    DateTime DueDate,
    RecurrenceFrequency? RecurrenceFrequency = null,
    DateTime? RecurrenceStartDate = null,
    DateTime? RecurrenceEndDate = null,
    string? Description = null);

public sealed record UpdateBillRequest(
    Guid BillId,
    string Title,
    decimal Amount,
    string Currency,
    BillCategory Category,
    DateTime DueDate,
    RecurrenceFrequency? RecurrenceFrequency = null,
    DateTime? RecurrenceStartDate = null,
    DateTime? RecurrenceEndDate = null,
    string? Description = null);

public sealed record DeactivateBillRequest(Guid BillId);

public sealed record ListBillsRequest(
    Guid HouseholdId,
    int Page = 1,
    int PageSize = 20,
    bool ActiveOnly = true);

public sealed record BillDetailRequest(Guid BillId);

public sealed record BillResponse(
    Guid BillId,
    Guid HouseholdId,
    string Title,
    string? Description,
    decimal Amount,
    string Currency,
    BillCategory Category,
    Guid CreatedBy,
    DateTime DueDate,
    RecurrenceFrequency? RecurrenceFrequency,
    DateTime? RecurrenceStartDate,
    DateTime? RecurrenceEndDate,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record BillListResponse(IReadOnlyCollection<BillResponse> Items, int TotalCount);

// Split enriched with member display name and role — avoids a separate members call
public sealed record SplitDetailResponse(
    Guid SplitId,
    Guid MembershipId,
    Guid UserId,
    string? DisplayName,
    string? AvatarUrl,
    string Role,
    decimal Amount,
    string Currency,
    bool IsClaimed,
    DateTime? ClaimedAt,
    Guid? ClaimedBy);

// Composite page response — bill + enriched splits + members + caller's role in one call
public sealed record BillPageResponse(
    BillResponse Bill,
    IReadOnlyCollection<SplitDetailResponse> Splits,
    IReadOnlyCollection<MembershipResponse> Members,
    string? CurrentUserRole);
