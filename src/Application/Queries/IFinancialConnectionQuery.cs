using Finance.Application.Dtos;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Queries;

public sealed record ListTransactionsParams(
    Guid ConnectionId,
    int Page = 1,
    int PageSize = 50);

public interface IFinancialConnectionQuery
{
    // ── Connections & transactions ────────────────────────────────────────────
    Task<ListConnectionsDto> ListConnectionsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<TransactionListDto> ListTransactionsAsync(
        Guid userId,
        ListTransactionsParams request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinancialAccount>> ListAccountsForConnectionAsync(FinancialConnectionId connectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringSuggestion>> ListSuggestionsForConnectionAsync(FinancialConnectionId connectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecurringSuggestion>> ListSuggestionsForUserAsync(UserId userId, CancellationToken cancellationToken = default);

    // ── Bank sync suggestions ─────────────────────────────────────────────────
    Task<ListSuggestionsDto> ListRecurringSuggestionsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ListBankSyncSuggestionsDto> ListForUserAsync(Guid userId, bool includeDismissed, CancellationToken cancellationToken = default);

    // ── Account balances ──────────────────────────────────────────────────────
    /// <summary>Returns the summed available balance across all linked depository accounts for a user.</summary>
    Task<AccountBalanceSummaryDto> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
