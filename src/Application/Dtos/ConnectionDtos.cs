using Finance.Domain.ValueObjects;

namespace Finance.Application.Dtos;

/// <summary>
/// Returned to the SPA when starting a bank-link session. The frontend passes
/// LinkToken straight to Plaid Link; nothing about the underlying vendor API is ever exposed to the browser.
/// </summary>
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

public sealed record ListConnectionsDto(IReadOnlyList<ConnectionDto> Connections);

/// <summary>Outcome of an incremental sync round-trip; surfaces counts so the UI can show what changed.</summary>
public sealed record SyncConnectionDto(
    Guid ConnectionId,
    int Added,
    int Modified,
    int Removed,
    bool HasMore,
    DateTime SyncedAt);

/// <summary>
/// Subset of the bank-link provider's webhook payload we care about.
/// Field names intentionally match the wire shape so model-binding works out of the box.
/// </summary>
public sealed record WebhookPayload(
    string WebhookType,
    string WebhookCode,
    string? ItemId,
    string? Error);

public sealed record RecurringSuggestionDto(
    Guid SuggestionId,
    Guid ConnectionId,
    Guid AccountId,
    string Direction,
    string Description,
    string? MerchantName,
    RecurrenceFrequency Frequency,
    decimal AverageAmount,
    decimal LastAmount,
    string Currency,
    DateTime FirstDate,
    DateTime LastDate,
    DateTime? PredictedNextDate,
    bool IsActive,
    bool IsLinked);

public sealed record ListSuggestionsDto(IReadOnlyList<RecurringSuggestionDto> Suggestions);

public sealed record AcceptSuggestionDto(
    Guid SuggestionId,
    Guid LinkedEntityId,
    string LinkedEntityType);

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

public sealed record BankSyncSuggestionDto(
    Guid SuggestionId,
    string Direction,
    string Name,
    string? MerchantName,
    decimal Amount,
    string Currency,
    DateTime TransactionDate,
    bool IsLinked,
    bool Dismissed);

public sealed record ListBankSyncSuggestionsDto(IReadOnlyList<BankSyncSuggestionDto> Suggestions);
