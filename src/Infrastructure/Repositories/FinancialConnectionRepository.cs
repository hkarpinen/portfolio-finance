using Finance.Domain.Aggregates;
using Finance.Domain.ReadModels;
using Finance.Application.Repositories;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

// Methods are deliberately fine-grained so the manager can sequence DB writes precisely. Concurrent
// sync collisions are absorbed by the unique index on external_transaction_id rather than by
// locking here.
internal sealed class FinancialConnectionRepository : IFinancialConnectionRepository
{
    private readonly FinanceDbContext _db;
    public FinancialConnectionRepository(FinanceDbContext db) => _db = db;

    public Task<FinancialConnection?> GetConnectionAsync(FinancialConnectionId id, CancellationToken ct = default)
        => _db.FinancialConnections.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<FinancialConnection?> GetConnectionByExternalIdAsync(string externalId, CancellationToken ct = default)
        => _db.FinancialConnections.FirstOrDefaultAsync(c => c.ExternalId == externalId, ct);

    public async Task AddConnectionAsync(FinancialConnection connection, CancellationToken ct = default)
        => await _db.FinancialConnections.AddAsync(connection, ct);

    public Task SaveConnectionAsync(FinancialConnection connection, CancellationToken ct = default)
    {
        _db.FinancialConnections.Update(connection);
        return Task.CompletedTask;
    }

    public Task RemoveConnectionAsync(FinancialConnection connection, CancellationToken ct = default)
    {
        _db.FinancialConnections.Remove(connection);
        return Task.CompletedTask;
    }

    public Task<FinancialAccount?> GetAccountByExternalIdAsync(FinancialConnectionId connectionId, string externalAccountId, CancellationToken ct = default)
        => _db.FinancialAccounts.FirstOrDefaultAsync(a => a.FinancialConnectionId == connectionId && a.ExternalAccountId == externalAccountId, ct);

    public async Task AddAccountAsync(FinancialAccount account, CancellationToken ct = default)
        => await _db.FinancialAccounts.AddAsync(account, ct);

    public Task SaveAccountAsync(FinancialAccount account, CancellationToken ct = default)
    {
        _db.FinancialAccounts.Update(account);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyDictionary<string, FinancialTransaction>> LookupTransactionsByExternalIdsAsync(
        IEnumerable<string> externalTransactionIds, CancellationToken ct = default)
    {
        var ids = externalTransactionIds.Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<string, FinancialTransaction>();
        var rows = await _db.FinancialTransactions
            .Where(t => ids.Contains(t.ExternalTransactionId))
            .ToListAsync(ct);
        return rows.ToDictionary(t => t.ExternalTransactionId, t => t);
    }

    public async Task AddTransactionAsync(FinancialTransaction transaction, CancellationToken ct = default)
        => await _db.FinancialTransactions.AddAsync(transaction, ct);

    public async Task RemoveTransactionByExternalIdAsync(string externalTransactionId, CancellationToken ct = default)
    {
        await _db.FinancialTransactions
            .Where(t => t.ExternalTransactionId == externalTransactionId)
            .ExecuteDeleteAsync(ct);
    }

    public Task CommitAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);

    public Task<RecurringSuggestion?> GetSuggestionByExternalIdAsync(string externalStreamId, CancellationToken ct = default)
        => _db.RecurringSuggestions.FirstOrDefaultAsync(s => s.ExternalStreamId == externalStreamId, ct);

    public Task<RecurringSuggestion?> GetSuggestionAsync(Guid id, CancellationToken ct = default)
        => _db.RecurringSuggestions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<RecurringSuggestion?> GetSuggestionByLinkedEntityIdAsync(Guid linkedEntityId, CancellationToken ct = default)
        => _db.RecurringSuggestions.FirstOrDefaultAsync(s => s.LinkedEntityId == linkedEntityId, ct);

    public async Task AddSuggestionAsync(RecurringSuggestion suggestion, CancellationToken ct = default)
        => await _db.RecurringSuggestions.AddAsync(suggestion, ct);

    public async Task SaveSuggestionAsync(RecurringSuggestion suggestion, CancellationToken ct = default)
    {
        _db.RecurringSuggestions.Update(suggestion);
    }

    public Task<BankSyncSuggestion?> GetBankSyncSuggestionByExternalTransactionIdAsync(string externalTransactionId, CancellationToken ct = default)
        => _db.BankSyncSuggestions.FirstOrDefaultAsync(s => s.ExternalTransactionId == externalTransactionId, ct);

    public Task<BankSyncSuggestion?> GetBankSyncSuggestionAsync(Guid id, CancellationToken ct = default)
        => _db.BankSyncSuggestions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task AddBankSyncSuggestionAsync(BankSyncSuggestion suggestion, CancellationToken ct = default)
        => await _db.BankSyncSuggestions.AddAsync(suggestion, ct);

    public Task SaveBankSyncSuggestionAsync(BankSyncSuggestion suggestion, CancellationToken ct = default)
    {
        _db.BankSyncSuggestions.Update(suggestion);
        return Task.CompletedTask;
    }
}
