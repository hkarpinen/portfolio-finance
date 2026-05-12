namespace Finance.Application.Commands;

/// <summary>
/// POSTed by the SPA after the bank-link flow's onSuccess callback fires.
/// The PublicToken is single-use and short-lived; we exchange it server-side
/// for the long-lived credential we encrypt and persist.
/// </summary>
public sealed record LinkConnectionCommand(
    string PublicToken,
    string? InstitutionId,
    string? InstitutionName);

public sealed record SyncConnectionCommand(Guid ConnectionId);

public sealed record RefreshSuggestionsCommand(Guid ConnectionId);

/// <summary>
/// Promotes a detected recurring cash-flow to a tracked IncomeSource (inflow)
/// or Expense (outflow). Idempotent — calling twice returns the same linked entity.
/// </summary>
public sealed record AcceptSuggestionCommand(Guid SuggestionId);

public sealed record DisconnectCommand(Guid ConnectionId);

/// <summary>
/// Accepts a bank-sync suggestion, creating an Expense (outflow) or IncomeSource (inflow).
/// AsIncome overrides the detected direction. HouseholdId creates a household-scoped expense.
/// </summary>
public sealed record AcceptBankSyncSuggestionCommand(
    Guid SuggestionId,
    bool AsIncome,
    Guid? HouseholdId = null);

public sealed record DismissBankSyncSuggestionCommand(Guid SuggestionId);
