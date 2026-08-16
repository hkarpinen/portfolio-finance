using Finance.Application.Commands;
using Finance.Application.Dtos;

namespace Finance.Application.Ports;

/// <summary>
/// Everything the app asks of a bank link: make one, sync it, offer what it noticed, drop it.
///
/// A port. Behind it is an adapter in Infrastructure holding the provider client and local mirrors
/// of what the provider said — accounts, transactions, suggestions. Those mirrors stay in
/// Infrastructure: they are another company's shapes, not domain concepts, and none of them posts
/// to the books. Only DTOs cross this line.
/// </summary>
public interface IBankConnections
{
    Task<LinkTokenDto> CreateLinkTokenAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ConnectionDto> ExchangePublicTokenAsync(Guid userId, LinkConnectionCommand request, CancellationToken cancellationToken = default);
    Task DisconnectAsync(Guid userId, DisconnectCommand request, CancellationToken cancellationToken = default);

    Task<SyncConnectionDto> SyncAsync(Guid userId, SyncConnectionCommand request, CancellationToken cancellationToken = default);
    Task SyncByExternalItemIdAsync(string externalItemId, CancellationToken cancellationToken = default);


}
