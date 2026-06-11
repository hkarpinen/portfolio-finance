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

// ── Settlement events ──────────────────────────────────────────────────────
// A settlement is a member (FromUserId, the debtor) settling their share into the
// funding account that fronted the bill (ToUserId, the payer). It lives as one journal
// entry in the group ledger; these events let consumers (e.g. household's activity feed)
// observe it. Reversal is a contra journal entry. Cross-service consumers declare matching
// types in Finance.Domain.Events — see memory finance-publishes-domain-events-directly.

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

