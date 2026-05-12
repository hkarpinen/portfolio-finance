using Finance.Domain.Aggregates;

namespace Finance.Application.Services;

/// <summary>
/// Cursor-based incremental transaction sync against the bank provider.
/// Owns the sync loop, bank-sync suggestion generation, recurring stream
/// auto-detection, auto-link, and auto-pay matching.
/// Change driver: Plaid sync protocol — cursor mechanics, transaction schema,
/// webhook event types, batch behaviour.
/// </summary>
internal interface IBankSyncService
{
    /// <summary>
    /// Runs the full cursor-based sync loop for the given connection.
    /// The caller is responsible for any auth checks before invoking this.
    /// </summary>
    Task<(int Added, int Modified, int Removed, bool HasMore)> SyncConnectionAsync(
        FinancialConnection connection,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches recurring streams from the bank provider for the given connection,
    /// upserts them as <see cref="RecurringSuggestion"/> records, and auto-links
    /// new suggestions to existing income / expense entities.
    /// </summary>
    Task RefreshSuggestionsAsync(
        FinancialConnection connection,
        CancellationToken ct = default);
}
