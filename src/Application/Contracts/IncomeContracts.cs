using Finance.Domain.ValueObjects;

namespace Finance.Application.Contracts;

public sealed record CreateIncomeRequest(
    Guid UserId,
    decimal Amount,
    string Currency,
    string Source,
    RecurrenceFrequency Frequency,
    DateTime StartDate,
    DateTime? EndDate = null,
    DateTime? LastPaymentDate = null);

public sealed record UpdateIncomeRequest(
    Guid IncomeId,
    decimal Amount,
    string Currency,
    string Source,
    RecurrenceFrequency Frequency,
    DateTime StartDate,
    DateTime? EndDate = null,
    DateTime? LastPaymentDate = null);

public sealed record IncomeDetailRequest(Guid IncomeId);

public sealed record DeleteIncomeRequest(Guid IncomeId);

public sealed record DeactivateIncomeRequest(Guid IncomeId);

public sealed record ListIncomeRequest(
    Guid HouseholdId,
    int Page = 1,
    int PageSize = 20,
    bool ActiveOnly = true);

public sealed record ListUserIncomeRequest(
    Guid UserId,
    int Page = 1,
    int PageSize = 20,
    bool ActiveOnly = true);

public sealed record IncomeResponse(
    Guid IncomeId,
    Guid UserId,
    decimal Amount,
    string Currency,
    string Source,
    RecurrenceFrequency Frequency,
    DateTime StartDate,
    DateTime? EndDate,
    bool IsActive,
    DateTime? LastPaymentDate,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record IncomeListResponse(IReadOnlyCollection<IncomeResponse> Items, int TotalCount);
