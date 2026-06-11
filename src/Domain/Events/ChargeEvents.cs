using Finance.Domain.ValueObjects;

namespace Finance.Domain.Events;

public record ChargeCreated(
    ChargeId ChargeId,
    UserId UserId,
    string Title,
    Money Amount,
    ChargeCategory Category,
    DateTime DueDate,
    RecurrenceSchedule? RecurrenceSchedule,
    GroupId? GroupId = null,
    Guid? PayerUserId = null) : DomainEvent;

public record ChargeUpdated(
    ChargeId ChargeId,
    string Title,
    Money Amount,
    ChargeCategory Category,
    DateTime DueDate,
    RecurrenceSchedule? RecurrenceSchedule,
    GroupId? GroupId = null,
    Guid? PayerUserId = null) : DomainEvent;

public record ChargeDeactivated(
    ChargeId ChargeId,
    GroupId? GroupId = null) : DomainEvent;

public record ChargePaid(
    ChargeId ChargeId,
    UserId UserId,
    DateTime OccurrenceDate,
    DateTime PaidAt) : DomainEvent;

public record ChargeActivated(
    ChargeId ChargeId,
    GroupId? GroupId = null) : DomainEvent;

public record ChargeUnpaid(
    ChargeId ChargeId,
    UserId UserId,
    DateTime OccurrenceDate) : DomainEvent;

/// <summary>The vendor was paid for an occurrence of a shared charge — fronted by a member
/// (<see cref="FundingSource.PayerMember"/>) or from the shared pot (<see cref="FundingSource.GroupCash"/>).</summary>
public record VendorPaid(
    ChargeId ChargeId,
    DateTime OccurrenceDate,
    FundingSource FundingSource,
    Guid? PaidByUserId,
    DateTime PaidAt) : DomainEvent;

public record VendorPaymentReversed(
    ChargeId ChargeId,
    DateTime OccurrenceDate) : DomainEvent;
