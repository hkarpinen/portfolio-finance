using Finance.Domain.ValueObjects;

namespace Finance.Domain.Events;

public record LedgerOpened(
    LedgerId LedgerId,
    AccountingEntity Owner,
    string Currency) : DomainEvent;

public record AccountOpened(
    AccountId AccountId,
    LedgerId LedgerId,
    string Code,
    AccountType AccountType) : DomainEvent;

public record JournalEntryPosted(
    JournalEntryId EntryId,
    LedgerId LedgerId,
    DateTime Date,
    string Description) : DomainEvent;

public record JournalEntryReversed(
    JournalEntryId EntryId,
    JournalEntryId ReversalOfEntryId,
    LedgerId LedgerId) : DomainEvent;
