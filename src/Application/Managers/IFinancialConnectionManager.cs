using Finance.Application.Commands;
using Finance.Application.Dtos;

namespace Finance.Application.Managers;

/// <summary>
/// Owns the full FinancialConnection command surface: link lifecycle, cursor-based
/// transaction sync, recurring-stream and bank-sync suggestion management.
/// Change driver: bank data provider API contract (Plaid Link, sync, recurring streams).
/// </summary>
public interface IFinancialConnectionManager
{
    // ── Link lifecycle ────────────────────────────────────────────────────────
    Task<LinkTokenDto> CreateLinkTokenAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ConnectionDto> ExchangePublicTokenAsync(Guid userId, LinkConnectionCommand request, CancellationToken cancellationToken = default);
    Task DisconnectAsync(Guid userId, DisconnectCommand request, CancellationToken cancellationToken = default);

    // ── Sync ──────────────────────────────────────────────────────────────────
    Task<SyncConnectionDto> SyncAsync(Guid userId, SyncConnectionCommand request, CancellationToken cancellationToken = default);
    Task SyncByExternalItemIdAsync(string externalItemId, CancellationToken cancellationToken = default);

    // ── Recurring suggestions ─────────────────────────────────────────────────
    Task<ListSuggestionsDto> RefreshSuggestionsAsync(Guid userId, RefreshSuggestionsCommand request, CancellationToken ct = default);
    Task<AcceptSuggestionDto> AcceptSuggestionAsync(Guid userId, AcceptSuggestionCommand request, CancellationToken ct = default);

    // ── Bank-sync suggestions ─────────────────────────────────────────────────
    Task<AcceptSuggestionDto> AcceptBankSyncSuggestionAsync(Guid userId, AcceptBankSyncSuggestionCommand request, CancellationToken ct = default);
    Task DismissBankSyncSuggestionAsync(Guid userId, DismissBankSyncSuggestionCommand request, CancellationToken ct = default);
}
