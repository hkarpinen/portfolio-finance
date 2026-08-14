using Finance.Domain.Aggregates;

namespace Finance.Application.Ports;

/// <summary>
/// Pulling what a bank knows into this service's own rows.
///
/// A port, not a manager. What sits behind it talks to a provider, maps its shapes onto ours and
/// writes them — an adapter, and it lives in Infrastructure with the client it wraps. It was in
/// Application under a Manager name, which made its I/O sequencing look like domain orchestration
/// and its private steps look like use cases that had gone astray.
/// </summary>
public interface IBankSynchroniser
{
    // The caller is responsible for any auth checks before invoking this.
    Task<(int Added, int Modified, int Removed, bool HasMore)> SyncConnectionAsync(
        FinancialConnection connection,
        CancellationToken cancellationToken = default);

    // Also auto-links new suggestions to existing income / expense entities.
    Task RefreshSuggestionsAsync(
        FinancialConnection connection,
        CancellationToken ct = default);

    /// <summary>
    /// Gives each of a connection's accounts a place in the owner's own books, carrying in the
    /// balance the provider reports. Without this a connected account is a row nobody can spend
    /// from, and every transaction on it has nowhere to post.
    ///
    /// Idempotent: an account already placed is left alone.
    /// </summary>
    Task PlaceAccountsInLedgerAsync(FinancialConnection connection, CancellationToken ct = default);
}
