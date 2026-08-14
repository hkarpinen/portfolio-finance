namespace Finance.Application.Commands;

// The PublicToken is single-use and short-lived; it is exchanged server-side for the long-lived
// credential that gets encrypted and persisted.
public sealed record LinkConnectionCommand(
    string PublicToken,
    string? InstitutionId,
    string? InstitutionName);

public sealed record SyncConnectionCommand(Guid ConnectionId);

public sealed record DisconnectCommand(Guid ConnectionId);
