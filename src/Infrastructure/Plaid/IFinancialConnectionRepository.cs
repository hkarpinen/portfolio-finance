using Infrastructure.Plaid.Mirrors;
using Finance.Domain.Aggregates;
using Infrastructure.Plaid.Mirrors;
using Finance.Domain.ValueObjects;

namespace Infrastructure.Plaid;

// Connections, accounts, transactions and recurring suggestions share one repository so the
// manager can hold a single EF transaction boundary across all of them.
//
// Child rows (accounts, transactions, suggestions) are removed by database cascade when a
// connection is deleted, so no explicit child-removal methods are needed here.
public interface IFinancialConnectionRepository
{
    Task<FinancialConnection?> GetConnectionAsync(FinancialConnectionId id, CancellationToken cancellationToken = default);

    Task<FinancialConnection?> GetConnectionByExternalIdAsync(string externalId, CancellationToken cancellationToken = default);

    Task AddConnectionAsync(FinancialConnection connection, CancellationToken cancellationToken = default);
    Task SaveConnectionAsync(FinancialConnection connection, CancellationToken cancellationToken = default);

    // Child rows cascade at the database level.
    Task RemoveConnectionAsync(FinancialConnection connection, CancellationToken cancellationToken = default);

    Task<FinancialAccount?> GetAccountByExternalIdAsync(FinancialConnectionId connectionId, string externalAccountId, CancellationToken cancellationToken = default);

    Task AddAccountAsync(FinancialAccount account, CancellationToken cancellationToken = default);
    Task SaveAccountAsync(FinancialAccount account, CancellationToken cancellationToken = default);


    Task<IReadOnlyDictionary<string, FinancialTransaction>> LookupTransactionsByExternalIdsAsync(
        IEnumerable<string> externalTransactionIds,
        CancellationToken cancellationToken = default);

    Task AddTransactionAsync(FinancialTransaction transaction, CancellationToken cancellationToken = default);

    Task RemoveTransactionByExternalIdAsync(string externalTransactionId, CancellationToken cancellationToken = default);

    Task CommitAsync(CancellationToken cancellationToken = default);

    Task<RecurringSuggestion?> GetSuggestionByExternalIdAsync(string externalStreamId, CancellationToken cancellationToken = default);

    Task<RecurringSuggestion?> GetSuggestionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RecurringSuggestion?> GetSuggestionByLinkedEntityIdAsync(Guid linkedEntityId, CancellationToken cancellationToken = default);
    Task AddSuggestionAsync(RecurringSuggestion suggestion, CancellationToken cancellationToken = default);
    Task SaveSuggestionAsync(RecurringSuggestion suggestion, CancellationToken cancellationToken = default);

    Task<BankSyncSuggestion?> GetBankSyncSuggestionByExternalTransactionIdAsync(string externalTransactionId, CancellationToken cancellationToken = default);
    Task<BankSyncSuggestion?> GetBankSyncSuggestionAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddBankSyncSuggestionAsync(BankSyncSuggestion suggestion, CancellationToken cancellationToken = default);
    Task SaveBankSyncSuggestionAsync(BankSyncSuggestion suggestion, CancellationToken cancellationToken = default);
}
