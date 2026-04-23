using Bills.Domain.ValueObjects;

namespace Bills.Domain.Events;

public record IncomeSourceCreated(
    IncomeId IncomeId,
    HouseholdId? HouseholdId,
    MembershipId? MembershipId,
    UserId UserId,
    Money Amount,
    string Source,
    RecurrenceSchedule RecurrenceSchedule) : DomainEvent;

public record IncomeSourceUpdated(
    IncomeId IncomeId,
    HouseholdId? HouseholdId,
    Money Amount,
    string Source) : DomainEvent;

public record IncomeSourceDeactivated(
    IncomeId IncomeId,
    HouseholdId? HouseholdId) : DomainEvent;
