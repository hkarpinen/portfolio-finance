using Finance.Application.Dtos;
using Finance.Application.Queries;
using Finance.Domain.Aggregates;
using Infrastructure.Plaid.Mirrors;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Plaid;

internal sealed class FinancialConnectionQuery : IFinancialConnectionQuery
{
    private readonly FinanceDbContext _db;

    public FinancialConnectionQuery(FinanceDbContext db) => _db = db;

    public async Task<ConnectionListDto> ListConnectionsAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var userIdVo = UserId.Create(userId);
        var connections = await _db.FinancialConnections
            .AsNoTracking()
            .Where(c => c.UserId == userIdVo)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        var connectionIds = connections.Select(c => c.Id).ToList();
        var allAccounts = await _db.FinancialAccounts
            .AsNoTracking()
            .Where(a => connectionIds.Contains(a.FinancialConnectionId))
            .ToListAsync(cancellationToken);

        var accountsByConnection = allAccounts
            .GroupBy(a => a.FinancialConnectionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var responses = connections.Select(c =>
        {
            accountsByConnection.TryGetValue(c.Id, out var accounts);
            return new ConnectionDto(
                c.Id.Value,
                c.InstitutionName,
                c.Status.ToString(),
                c.LastSyncedAt,
                c.CreatedAt,
                (accounts ?? []).Select(a => new LinkedAccountDto(
                    a.Id, a.Name, a.OfficialName, a.Mask, a.Type, a.Subtype,
                    a.CurrencyCode, a.CurrentBalance, a.AvailableBalance)).ToList());
        }).ToList();

        return new ConnectionListDto(responses, responses.Count);
    }

    public async Task<TransactionListDto> ListTransactionsAsync(
        Guid userId, ListTransactionsParams request, CancellationToken cancellationToken = default)
    {
        var connectionId = FinancialConnectionId.Create(request.ConnectionId);
        var connection = await _db.FinancialConnections
            .AsNoTracking()
            .Where(c => c.Id == connectionId)
            .Select(c => new { c.Id, c.UserId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException("Financial connection not found.");

        if (connection.UserId.Value != userId)
            throw new UnauthorizedAccessException("Access denied.");

        var page     = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 200);
        var query    = _db.FinancialTransactions.AsNoTracking().Where(t => t.FinancialConnectionId == connection.Id);
        var total    = await query.CountAsync(cancellationToken);
        var rows     = await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = rows.Select(t => new TransactionDto(
            t.Id, t.AccountId,
            t.Amount.Amount, t.Amount.Currency,
            t.Date, t.Name, t.MerchantName, t.PrimaryCategory,
            t.Pending, t.LinkedEntityId.HasValue)).ToList();

        return new TransactionListDto(dtos, total);
    }

    public async Task<IReadOnlyList<FinancialAccount>> ListAccountsForConnectionAsync(
        FinancialConnectionId connectionId, CancellationToken cancellationToken = default)
        => await _db.FinancialAccounts
            .AsNoTracking()
            .Where(a => a.FinancialConnectionId == connectionId)
            .ToListAsync(cancellationToken);

    public async Task<AccountBalanceSummaryDto> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userIdVo = new UserId(userId);
        var accounts = await _db.FinancialAccounts
            .AsNoTracking()
            .Where(a => a.UserId == userIdVo && a.IsActive)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

        if (accounts.Count == 0)
            return new AccountBalanceSummaryDto(null, null, null, false, []);

        var spendableAccounts = accounts
            .Where(a => string.Equals(a.Type, "depository", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var currency = spendableAccounts.FirstOrDefault()?.CurrencyCode ?? accounts[0].CurrencyCode;
        decimal? totalAvailable = spendableAccounts.Any(a => a.AvailableBalance.HasValue)
            ? spendableAccounts.Sum(a => a.AvailableBalance ?? 0m)
            : null;
        var asOf = accounts.Max(a => (DateTime?)a.UpdatedAt);
        var dtos = accounts.Select(a => new LinkedAccountBalanceDto(
            a.Id, a.Name, a.Mask, a.Type,
            a.AvailableBalance, a.CurrentBalance, a.CurrencyCode)).ToList();

        return new AccountBalanceSummaryDto(totalAvailable, currency, asOf, true, dtos);
    }

    public async Task<IReadOnlyList<FinancialTransaction>> ListImportedTransactionsAsync(
        FinancialConnectionId connectionId, CancellationToken ct = default)
        => await _db.FinancialTransactions.AsNoTracking()
            .Where(x => x.FinancialConnectionId == connectionId && x.LinkedEntityId != null)
            .ToListAsync(ct);
}
