using Finance.Application.Commands;
using Finance.Application.Dtos;

namespace Finance.Application.Managers;

public interface IFinancialConnectionManager
{
    Task<LinkTokenDto> CreateLinkTokenAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ConnectionDto> ExchangePublicTokenAsync(Guid userId, LinkConnectionCommand request, CancellationToken cancellationToken = default);
    Task DisconnectAsync(Guid userId, DisconnectCommand request, CancellationToken cancellationToken = default);

    Task<SyncConnectionDto> SyncAsync(Guid userId, SyncConnectionCommand request, CancellationToken cancellationToken = default);
    Task SyncByExternalItemIdAsync(string externalItemId, CancellationToken cancellationToken = default);

    Task<RecurringSuggestionListDto> RefreshSuggestionsAsync(Guid userId, RefreshSuggestionsCommand request, CancellationToken ct = default);
    Task<AcceptSuggestionDto> AcceptSuggestionAsync(Guid userId, AcceptSuggestionCommand request, CancellationToken ct = default);

    Task<AcceptSuggestionDto> AcceptBankSyncSuggestionAsync(Guid userId, AcceptBankSyncSuggestionCommand request, CancellationToken ct = default);
    Task DismissBankSyncSuggestionAsync(Guid userId, DismissBankSyncSuggestionCommand request, CancellationToken ct = default);
}
