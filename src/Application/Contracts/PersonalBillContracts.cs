using Finance.Domain.ValueObjects;

namespace Finance.Application.Contracts;

public sealed record CreatePersonalBillRequest(
    Guid UserId,
    string Title,
    decimal Amount,
    string Currency,
    string Category,
    DateTime DueDate,
    string? RecurrenceFrequency = null,
    DateTime? RecurrenceStartDate = null,
    DateTime? RecurrenceEndDate = null,
    string? Description = null);

public sealed record UpdatePersonalBillRequest(
    Guid PersonalBillId,
    string Title,
    decimal Amount,
    string Currency,
    string Category,
    DateTime DueDate,
    string? RecurrenceFrequency = null,
    DateTime? RecurrenceStartDate = null,
    DateTime? RecurrenceEndDate = null,
    string? Description = null);

public sealed record DeletePersonalBillRequest(Guid PersonalBillId);

public sealed record PersonalBillDetailRequest(Guid PersonalBillId);

public sealed record ListPersonalBillsRequest(
    Guid UserId,
    int Page = 1,
    int PageSize = 50,
    bool ActiveOnly = true);

public sealed record PersonalBillResponse(
    Guid PersonalBillId,
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
    DateTime UpdatedAt);

public sealed record PersonalBillListResponse(IReadOnlyCollection<PersonalBillResponse> Items, int TotalCount);
