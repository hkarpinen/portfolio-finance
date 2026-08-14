using Finance.Domain.ValueObjects;

namespace Finance.Application.Dtos;

// Nothing about the underlying vendor API is ever exposed to the browser — only this token.
public sealed record LinkTokenDto(string LinkToken, DateTime Expiration);

public sealed record ConnectionDto(
    Guid ConnectionId,
    string InstitutionName,
    string Status,
    DateTime? LastSyncedAt,
    DateTime CreatedAt,
    IReadOnlyList<LinkedAccountDto> Accounts);

public sealed record LinkedAccountDto(
    Guid AccountId,
    string Name,
    string? OfficialName,
    string? Mask,
    string Type,
    string? Subtype,
    string Currency,
    decimal? CurrentBalance,
    decimal? AvailableBalance);

public sealed record ConnectionListDto(IReadOnlyList<ConnectionDto> Items, int TotalCount);

public sealed record SyncConnectionDto(
    Guid ConnectionId,
    int Added,
    int Modified,
    int Removed,
    bool HasMore,
    DateTime SyncedAt);

// Field names intentionally match the wire shape so model-binding works without attributes.
public sealed record WebhookPayload(
    string WebhookType,
    string WebhookCode,
    string? ItemId,
    string? Error);

public sealed record TransactionDto(
    Guid TransactionId,
    Guid AccountId,
    decimal Amount,
    string Currency,
    DateTime Date,
    string Name,
    string? MerchantName,
    string? PrimaryCategory,
    bool Pending,
    bool IsLinked);

public sealed record TransactionListDto(
    IReadOnlyCollection<TransactionDto> Items,
    int TotalCount);
