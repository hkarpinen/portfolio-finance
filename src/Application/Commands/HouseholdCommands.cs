namespace Finance.Application.Commands;

public sealed record CreateHouseholdCommand(
    string Name,
    Guid OwnerId,
    string CurrencyCode = "USD",
    string? Description = null);

public sealed record UpdateHouseholdCommand(
    Guid HouseholdId,
    string Name,
    string? Description = null);

public sealed record TransferHouseholdOwnershipCommand(
    Guid HouseholdId,
    Guid NewOwnerId,
    Guid RequestingUserId);

public sealed record DeleteHouseholdCommand(
    Guid HouseholdId,
    Guid RequestingUserId);
