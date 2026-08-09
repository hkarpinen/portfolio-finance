using Finance.Domain.ValueObjects;

namespace Finance.Domain.Events;

public record AllocationCreated(
    AllocationId AllocationId,
    ChargeId ChargeId,
    GroupId GroupId,
    UserId UserId,
    Money Amount) : DomainEvent;

public record AllocationUpdated(
    AllocationId AllocationId,
    ChargeId ChargeId,
    GroupId GroupId,
    Money NewAmount) : DomainEvent;

public record AllocationRemoved(
    AllocationId AllocationId,
    ChargeId ChargeId,
    GroupId GroupId) : DomainEvent;

// A settlement is the member FromUserId (the debtor) settling their share into the funding account
// that fronted the bill, held by ToUserId (the payer). It is one journal entry in the group ledger;
// a reversal is a contra entry.

public record SettlementRecorded(
    AllocationId AllocationId,
    ChargeId ChargeId,
    GroupId GroupId,
    UserId FromUserId,
    UserId ToUserId,
    Money Amount,
    DateTime OccurrenceDate,
    DateTime ValueDate) : DomainEvent;

public record SettlementReversed(
    AllocationId AllocationId,
    ChargeId ChargeId,
    GroupId GroupId,
    UserId FromUserId,
    DateTime OccurrenceDate) : DomainEvent;

