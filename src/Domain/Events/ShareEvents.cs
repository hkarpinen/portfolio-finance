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
// that fronted the expense, held by ToUserId (the payer). It is one journal entry in the group ledger;
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

/// <summary>
/// One member paid another directly to square up. Carries everything the books need, so the
/// consumer never reloads the document — and nothing about an expense, because there isn't one.
/// </summary>
public record MemberTransferRecorded(
    MemberTransferId TransferId,
    GroupId GroupId,
    UserId FromUserId,
    UserId ToUserId,
    Money Amount,
    DateTime OccurredOn) : DomainEvent;
