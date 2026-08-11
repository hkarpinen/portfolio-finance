namespace Finance.Domain.Events;

public sealed record ChargeScheduleCreated(
    Guid ScheduleId,
    Guid UserId,
    Guid? GroupId,
    string Title,
    decimal Amount,
    string Currency,
    string Category,
    string Frequency,
    DateTime AnchorDate,
    DateTime OccurredAt) : DomainEvent;

public sealed record ChargeScheduleAmended(
    Guid ScheduleId,
    string Title,
    decimal Amount,
    string Currency,
    string Category,
    DateTime OccurredAt) : DomainEvent;

public sealed record ChargeScheduleDeactivated(
    Guid ScheduleId,
    DateTime OccurredAt) : DomainEvent;
