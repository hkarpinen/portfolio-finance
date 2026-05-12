using Finance.Domain.ValueObjects;

namespace Finance.Domain.Events;

public sealed record FinancialConnectionEstablished(
    FinancialConnectionId ConnectionId,
    UserId UserId,
    string InstitutionName) : DomainEvent;

public sealed record FinancialConnectionRequiresReauth(
    FinancialConnectionId ConnectionId,
    UserId UserId) : DomainEvent;

public sealed record FinancialConnectionRevoked(
    FinancialConnectionId ConnectionId,
    UserId UserId) : DomainEvent;

public sealed record FinancialConnectionSynced(
    FinancialConnectionId ConnectionId,
    UserId UserId,
    int Added,
    int Modified,
    int Removed) : DomainEvent;
