using Infrastructure.Plaid.Mirrors;
using Finance.Application.Commands;
using Finance.Application.Dtos;
using Finance.Application.Mappers;
using Finance.Application.Ports;
using Finance.Application.Queries;
using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Infrastructure.Plaid.Mirrors;
using Finance.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Plaid;

internal sealed class FinancialConnectionService : IBankConnections
{
    private readonly IBankDataProvider _api;
    private readonly IFinancialConnectionRepository _repo;
    private readonly IFinancialConnectionQuery _connectionQuery;
    private readonly IConnectionTokenProtector _tokenProtector;
    private readonly IExpenseRepository _expenseRepository;
    private readonly IReceiptRepository _receipts;
    private readonly IBankSynchroniser _syncService;
    private readonly ILogger<FinancialConnectionService> _logger;

    public FinancialConnectionService(
        IBankDataProvider api,
        IFinancialConnectionRepository repo,
        IFinancialConnectionQuery connectionQuery,
        IConnectionTokenProtector tokenProtector,
        IExpenseRepository expenseRepository,
        IReceiptRepository receipts,
        IBankSynchroniser syncService,
        ILogger<FinancialConnectionService> logger)
    {
        _api = api;
        _repo = repo;
        _connectionQuery = connectionQuery;
        _tokenProtector = tokenProtector;
        _expenseRepository = expenseRepository;
        _receipts = receipts;
        _syncService = syncService;
        _logger = logger;
    }

    public async Task<LinkTokenDto> CreateLinkTokenAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var result = await _api.CreateLinkTokenAsync(userId, cancellationToken);
        return FinancialConnectionMapper.ToLinkToken(result.LinkToken, result.Expiration);
    }

    public async Task<ConnectionDto> ExchangePublicTokenAsync(
        Guid userId, LinkConnectionCommand request, CancellationToken cancellationToken = default)
    {
        var exchange = await _api.ExchangePublicTokenAsync(request.PublicToken, cancellationToken);

        // Idempotency: re-linking the same institution updates in place rather than creating a duplicate.
        // Plaid issues a new access_token on every exchange, so we always store the freshest one.
        var existing = await _repo.GetConnectionByExternalIdAsync(exchange.ItemId, cancellationToken);
        var encrypted = _tokenProtector.Protect(exchange.AccessToken);
        FinancialConnection connection;

        if (existing is not null)
        {
            // Whatever the old connection's transactions became must come off the books before
            // the connection does — afterwards there is nothing left to find them by.
            await UnimportEverythingAsync(existing.Id, cancellationToken);

            // Re-link is modelled as remove + add because FinancialConnection has no public setter for
            // EncryptedAccessToken (intentionally invariant-protected).
            await _repo.RemoveConnectionAsync(existing, cancellationToken);
        }

        connection = FinancialConnection.Connect(
            UserId.Create(userId),
            exchange.ItemId,
            request.InstitutionName ?? "Unknown",
            request.InstitutionId,
            encrypted);

        await _repo.AddConnectionAsync(connection, cancellationToken);
        await _repo.CommitAsync(cancellationToken);

        var accountsResult = await _api.GetAccountsAsync(exchange.AccessToken, cancellationToken);
        var persistedAccounts = new List<FinancialAccount>(accountsResult.Accounts.Count);
        foreach (var dto in accountsResult.Accounts)
        {
            var account = FinancialConnectionMapper.ToAccount(connection, dto);
            await _repo.AddAccountAsync(account, cancellationToken);
            persistedAccounts.Add(account);
        }
        await _repo.CommitAsync(cancellationToken);

        try
        {
            await _syncService.SyncConnectionAsync(connection, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Initial sync after linking connection {ConnectionId} failed; will be retried on webhook.",
                connection.ExternalId);
        }

        return FinancialConnectionMapper.ToResponse(connection, persistedAccounts);
    }

    /// <summary>
    /// Takes every document a connection's transactions became off the books, before the
    /// connection itself goes. An expense whose bank link is gone is still a real cost somebody
    /// incurred, so it is deactivated rather than deleted — and a receipt voided — which lets the
    /// ledger unwind what it posted instead of leaving entries pointing at nothing.
    /// </summary>
    private async Task UnimportEverythingAsync(FinancialConnectionId connectionId, CancellationToken ct)
    {
        var transactions = await _connectionQuery.ListImportedTransactionsAsync(connectionId, ct);

        foreach (var txn in transactions)
        {
            if (txn.LinkedEntityId is not { } entityId) continue;

            if (txn.LinkedEntityType == LinkedEntityType.Receipt)
            {
                var receipt = await _receipts.GetByIdAsync(ReceiptId.Create(entityId), ct);
                if (receipt is null) continue;
                receipt.Void();
                await _receipts.UpdateAsync(receipt, ct);
                continue;
            }

            var expense = await _expenseRepository.GetByIdAsync(ExpenseId.Create(entityId), ct);
            if (expense is null) continue;
            expense.TryDeactivate();
            await _expenseRepository.UpdateAsync(expense, ct);
        }
    }

    public async Task DisconnectAsync(
        Guid userId, DisconnectCommand request, CancellationToken cancellationToken = default)
    {
        var connection = await _repo.GetConnectionAsync(FinancialConnectionId.Create(request.ConnectionId), cancellationToken);
        if (connection is null) return;
        if (connection.UserId.Value != userId)
            throw new UnauthorizedAccessException("Access denied.");

        try
        {
            var accessToken = _tokenProtector.Unprotect(connection.EncryptedAccessToken);
            await _api.RemoveItemAsync(accessToken, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Provider item removal failed for connection {ConnectionId}", connection.ExternalId);
        }

        await UnimportEverythingAsync(connection.Id, cancellationToken);

        // Removing the connection cascades to accounts, transactions and suggestions at the DB level.
        connection.MarkRevoked();
        await _repo.RemoveConnectionAsync(connection, cancellationToken);
        await _repo.CommitAsync(cancellationToken);
    }

    public async Task<SyncConnectionDto> SyncAsync(
        Guid userId, SyncConnectionCommand request, CancellationToken cancellationToken = default)
    {
        var connection = await _repo.GetConnectionAsync(
            FinancialConnectionId.Create(request.ConnectionId), cancellationToken)
            ?? throw new KeyNotFoundException("Financial connection not found.");

        if (connection.UserId.Value != userId)
            throw new UnauthorizedAccessException("Access denied.");

        var (added, modified, removed, hasMore) = await _syncService.SyncConnectionAsync(connection, cancellationToken);
        return FinancialConnectionMapper.ToSyncResult(
            connection.Id.Value, added, modified, removed, hasMore, DateTime.UtcNow);
    }

    public async Task SyncByExternalItemIdAsync(
        string externalItemId, CancellationToken cancellationToken = default)
    {
        var connection = await _repo.GetConnectionByExternalIdAsync(externalItemId, cancellationToken);
        if (connection is null)
        {
            _logger.LogWarning("Webhook received for unknown provider item id {ExternalItemId}", externalItemId);
            return;
        }
        connection.RecordWebhook();
        await _syncService.SyncConnectionAsync(connection, cancellationToken);
    }


}
