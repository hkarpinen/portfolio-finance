using Finance.Domain.ValueObjects;

namespace Finance.Application.Ports;

// Every type in this file uses domain-oriented names — no vendor terminology crosses this port.
public interface IBankDataProvider
{
    Task<BankLinkToken> CreateLinkTokenAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<BankConnectionCredentials> ExchangePublicTokenAsync(
        string publicToken,
        CancellationToken cancellationToken = default);

    Task<ExternalAccountsResult> GetAccountsAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    Task<TransactionSyncPage> SyncTransactionsAsync(
        string accessToken,
        string? cursor,
        CancellationToken cancellationToken = default);

    Task<RecurringStreamsResult> GetRecurringTransactionsAsync(
        string accessToken,
        CancellationToken cancellationToken = default);

    Task RemoveItemAsync(string accessToken, CancellationToken cancellationToken = default);
}

public interface IConnectionTokenProtector
{
    string Protect(string accessToken);
    string Unprotect(string encryptedAccessToken);
}


public sealed record BankLinkToken(string LinkToken, DateTime Expiration);

public sealed record BankConnectionCredentials(string AccessToken, string ItemId);

public sealed record ExternalAccountsResult(IReadOnlyList<ExternalAccountDto> Accounts);

public sealed record ExternalAccountDto(
    string AccountId,
    string Name,
    string? OfficialName,
    string? Mask,
    string Type,
    string? Subtype,
    string CurrencyCode,
    decimal? CurrentBalance,
    decimal? AvailableBalance);

public sealed record TransactionSyncPage(
    IReadOnlyList<ExternalTransactionDto> Added,
    IReadOnlyList<ExternalTransactionDto> Modified,
    IReadOnlyList<string> Removed,
    string NextCursor,
    bool HasMore);

public sealed record ExternalTransactionDto(
    string TransactionId,
    string AccountId,
    decimal Amount,
    string Currency,
    DateTime Date,
    DateTime? AuthorizedDate,
    string Name,
    string? MerchantName,
    string? PrimaryCategory,
    string? DetailedCategory,
    bool Pending);

public sealed record RecurringStreamsResult(
    IReadOnlyList<RecurringStreamDto> Inflow,
    IReadOnlyList<RecurringStreamDto> Outflow);

public sealed record RecurringStreamDto(
    string StreamId,
    string AccountId,
    string Description,
    string? MerchantName,
    RecurrenceFrequency Frequency,
    decimal AverageAmount,
    decimal LastAmount,
    string Currency,
    DateTime FirstDate,
    DateTime LastDate,
    DateTime? PredictedNextDate,
    bool IsActive);
