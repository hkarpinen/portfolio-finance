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

    public static RecurringSuggestionDto ToSuggestion(RecurringSuggestion s) => new(
        s.Id, s.FinancialConnectionId.Value, s.AccountId,
        s.Direction.ToString(), s.Description, s.MerchantName,
        s.Frequency, s.AverageAmount.Amount, s.LastAmount.Amount, s.AverageAmount.Currency,
        s.FirstDate, s.LastDate, s.PredictedNextDate, s.IsActive, s.IsLinked);

    public static RecurringSuggestionListDto ToSuggestionList(IEnumerable<RecurringSuggestion> suggestions)
    {
        var items = suggestions.Select(ToSuggestion).ToList();
        return new RecurringSuggestionListDto(items, items.Count);
    }

    /// <summary>What a suggestion ended up linked to — a new income source, a new expense, or the
    /// one it was already attached to.</summary>
    /// <summary>What an already-linked suggestion became. Takes the suggestion: its id, the thing
    /// it links to and what kind that is are three of its own fields, and only it knows they agree.</summary>
    public static AcceptSuggestionDto ToAccepted(RecurringSuggestion suggestion) =>
        ToAccepted(suggestion.Id, suggestion.LinkedEntityId!.Value, suggestion.LinkedEntityType!);

    public static AcceptSuggestionDto ToAccepted(BankSyncSuggestion suggestion) =>
        ToAccepted(suggestion.Id, suggestion.LinkedEntityId!.Value, suggestion.LinkedEntityType!);

    public static AcceptSuggestionDto ToAccepted(Guid suggestionId, Guid entityId, string linkedEntityType) =>
        new(suggestionId, entityId, linkedEntityType);

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
