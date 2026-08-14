using Finance.Domain.ValueObjects;

namespace Finance.Domain.Events;

public record ShareCreated(
    ShareId ShareId,
    ExpenseId ExpenseId,
    GroupId GroupId,
    UserId UserId,
    Money Amount) : DomainEvent;

public record ShareUpdated(
    ShareId ShareId,
    ExpenseId ExpenseId,
    GroupId GroupId,
    Money NewAmount) : DomainEvent;

public record ShareRemoved(
    ShareId ShareId,
    ExpenseId ExpenseId,
    GroupId GroupId) : DomainEvent;

// A settlement is the member FromUserId (the debtor) settling their share into the funding account
// that fronted the bill, held by ToUserId (the payer). It is one journal entry in the group ledger;
// a reversal is a contra entry.

public record SettlementRecorded(
    ShareId ShareId,
    ExpenseId ExpenseId,
    GroupId GroupId,
    UserId FromUserId,
    UserId ToUserId,
    Money Amount,
    DateTime OccurrenceDate,
    DateTime ValueDate) : DomainEvent;

public record SettlementReversed(
    ShareId ShareId,
    ExpenseId ExpenseId,
    GroupId GroupId,
    UserId FromUserId,
    DateTime OccurrenceDate) : DomainEvent;

