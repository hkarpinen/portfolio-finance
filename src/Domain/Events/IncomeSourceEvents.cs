using Bills.Domain.ValueObjects;

namespace Bills.Domain.Events;

public record IncomeSourceCreated(
    IncomeId IncomeId,
    UserId UserId,
    Money Amount,
    string Source,
    RecurrenceSchedule RecurrenceSchedule) : DomainEvent;

public record IncomeSourceUpdated(
    IncomeId IncomeId,
    Money Amount,
    string Source) : DomainEvent;

public record IncomeSourceDeactivated(
    IncomeId IncomeId) : DomainEvent;
