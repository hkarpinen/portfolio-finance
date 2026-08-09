namespace Finance.Application.Commands;

// The PublicToken is single-use and short-lived; it is exchanged server-side for the long-lived
// credential that gets encrypted and persisted.
public sealed record LinkConnectionCommand(
    string PublicToken,
    string? InstitutionId,
    string? InstitutionName);

public sealed record SyncConnectionCommand(Guid ConnectionId);

public sealed record RefreshSuggestionsCommand(Guid ConnectionId);

// Idempotent — calling twice returns the same linked entity.
public sealed record AcceptSuggestionCommand(Guid SuggestionId);

public sealed record DisconnectCommand(Guid ConnectionId);

// AsIncome overrides the detected direction; GroupId makes the result household-scoped.
public sealed record AcceptBankSyncSuggestionCommand(
    Guid SuggestionId,
    bool AsIncome,
    Guid? GroupId = null);

public sealed record DismissBankSyncSuggestionCommand(Guid SuggestionId);
