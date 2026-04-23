namespace Bills.Application.Contracts;

public sealed record UpsertSplitRequest(
    Guid? SplitId,
    Guid BillId,
    Guid HouseholdId,
    Guid MembershipId,
    Guid UserId,
    decimal Amount,
    string Currency);

public sealed record RemoveSplitRequest(Guid SplitId);

public sealed record ListSplitsRequest(Guid BillId);

public sealed record SplitResponse(
    Guid SplitId,
    Guid BillId,
    Guid HouseholdId,
    Guid MembershipId,
    Guid UserId,
    decimal Amount,
    string Currency,
    bool IsClaimed,
    DateTime? ClaimedAt,
    Guid? ClaimedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt);
