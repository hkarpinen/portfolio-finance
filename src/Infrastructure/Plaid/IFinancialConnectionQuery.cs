using Infrastructure.Plaid.Mirrors;
using Finance.Application.Dtos;
using Finance.Domain.Aggregates;
using Infrastructure.Plaid.Mirrors;
using Finance.Domain.ValueObjects;

namespace Infrastructure.Plaid;

public sealed record ListTransactionsParams(
    Guid ConnectionId,
    int Page = 1,
    int PageSize = 50);

public interface IFinancialConnectionQuery
{
    Task<ConnectionListDto> ListConnectionsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<TransactionListDto> ListTransactionsAsync(
        Guid userId,
        ListTransactionsParams request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancialAccount>> ListAccountsForConnectionAsync(FinancialConnectionId connectionId, CancellationToken cancellationToken = default);


    Task<AccountBalanceSummaryDto> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Every transaction on a connection that became a document, so the documents can be
    /// found when the connection goes.</summary>
    Task<IReadOnlyList<FinancialTransaction>> ListImportedTransactionsAsync(
        FinancialConnectionId connectionId, CancellationToken ct = default);
}
