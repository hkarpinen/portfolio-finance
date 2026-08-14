using Finance.Application.Ports;
using Infrastructure.Plaid.Mirrors;
using Finance.Application.Dtos;
using Finance.Domain.Aggregates;
using Infrastructure.Plaid.Mirrors;

namespace Infrastructure.Plaid;

public static class FinancialConnectionMapper
{
    public static LinkTokenDto ToLinkToken(string linkToken, DateTime expiration) =>
        new(linkToken, expiration);

    /// <summary>What one sync moved. `syncedAt` is passed in rather than read here — a mapper
    /// that reads the clock is a mapper whose output nobody can pin down in a test.</summary>
    public static SyncConnectionDto ToSyncResult(
        Guid connectionId, int added, int modified, int removed, bool hasMore, DateTime syncedAt) =>
        new(connectionId, added, modified, removed, hasMore, syncedAt);

    public static ConnectionDto ToResponse(
        FinancialConnection connection, IEnumerable<FinancialAccount> accounts) => new(
        connection.Id.Value,
        connection.InstitutionName,
        connection.Status.ToString(),
        connection.LastSyncedAt,
        connection.CreatedAt,
        accounts.Select(ToLinkedAccount).ToList());

    public static LinkedAccountDto ToLinkedAccount(FinancialAccount account) => new(
        account.Id, account.Name, account.OfficialName, account.Mask, account.Type,
        account.Subtype, account.CurrencyCode, account.CurrentBalance, account.AvailableBalance);

    /// <summary>
    /// A provider's account shape turned into the local mirror of it.
    ///
    /// Eleven arguments, transcribed identically in two places — the first sync and every one
    /// after. Both directions of this file are mapping; only the outbound one had a home, which is
    /// why the inbound one got written twice.
    /// </summary>
    public static FinancialAccount ToAccount(FinancialConnection connection, ExternalAccountDto dto) =>
        FinancialAccount.Create(
            connection.Id, connection.UserId, dto.AccountId, dto.Name, dto.OfficialName, dto.Mask,
            dto.Type, dto.Subtype, dto.CurrencyCode, dto.CurrentBalance, dto.AvailableBalance);
}
