using Finance.Domain.Aggregates;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

/// <summary>
/// Repository surface for the financial connection aggregate cluster.
/// Combines connections, accounts, transactions and recurring suggestions into one
/// repository so the manager can share an EF transaction boundary across all writes.
///
/// Child rows (accounts, transactions, suggestions) are removed automatically via
/// database cascade when a connection is deleted — no explicit child-removal methods
/// are needed on this interface.
/// </summary>
public interface IFinancialConnectionRepository
{
    // ── Connections ───────────────────────────────────────────────────────────
    Task<FinancialConnection?> GetConnectionAsync(FinancialConnectionId id, CancellationToken cancellationToken = default);

    /// <summary>Looks up a connection by the opaque identifier issued by the financial data provider.</summary>
    Task<FinancialConnection?> GetConnectionByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);

    Task AddConnectionAsync(FinancialConnection connection, CancellationToken cancellationToken = default);
    Task SaveConnectionAsync(FinancialConnection connection, CancellationToken cancellationToken = default);

    /// <summary>Removes the connection; child rows cascade at the database level.</summary>
    Task RemoveConnectionAsync(FinancialConnection connection, CancellationToken cancellationToken = default);

    // ── Accounts ──────────────────────────────────────────────────────────────
    /// <summary>Looks up an account by the provider-issued account identifier.</summary>
    Task<FinancialAccount?> GetAccountByExternalIdAsync(FinancialConnectionId connectionId, string externalAccountId, CancellationToken cancellationToken = default);

    Task AddAccountAsync(FinancialAccount account, CancellationToken cancellationToken = default);
    Task SaveAccountAsync(FinancialAccount account, CancellationToken cancellationToken = default);

    // ── Transactions (tuned for the high-volume sync path) ────────────────────

    /// <summary>
    /// Batch lookup by provider-issued transaction ids. Used during sync to resolve
    /// modified and removed transactions in a single round-trip.
    /// </summary>
    Task<IReadOnlyDictionary<string, FinancialTransaction>> LookupTransactionsByExternalIdsAsync(
        IEnumerable<string> externalTransactionIds,
        CancellationToken cancellationToken = default);

    Task AddTransactionAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default);

    /// <summary>Removes a transaction by its provider-issued id (used for provider-side deletions during sync).</summary>
    Task RemoveTransactionByExternalIdAsync(string externalTransactionId, CancellationToken cancellationToken = default);

    /// <summary>Persists every pending change in the unit of work in a single round-trip.</summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    // ── Recurring suggestions ─────────────────────────────────────────────────

    /// <summary>Looks up a suggestion by the provider-issued stream identifier (upsert key).</summary>
    Task<RecurringSuggestion?> GetSuggestionByExternalIdAsync(string externalStreamId, CancellationToken cancellationToken = default);

    Task<RecurringSuggestion?> GetSuggestionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RecurringSuggestion?> GetSuggestionByLinkedEntityIdAsync(Guid linkedEntityId, CancellationToken cancellationToken = default);
    Task AddSuggestionAsync(RecurringSuggestion suggestion, CancellationToken cancellationToken = default);
    Task SaveSuggestionAsync(RecurringSuggestion suggestion, CancellationToken cancellationToken = default);

    // ── Bank sync suggestions ─────────────────────────────────────────────────

    Task<BankSyncSuggestion?> GetBankSyncSuggestionByExternalTransactionIdAsync(string externalTransactionId, CancellationToken cancellationToken = default);
    Task<BankSyncSuggestion?> GetBankSyncSuggestionAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddBankSyncSuggestionAsync(BankSyncSuggestion suggestion, CancellationToken cancellationToken = default);
    Task SaveBankSyncSuggestionAsync(BankSyncSuggestion suggestion, CancellationToken cancellationToken = default);
}
