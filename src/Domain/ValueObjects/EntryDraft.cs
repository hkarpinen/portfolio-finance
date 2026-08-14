namespace Finance.Domain.ValueObjects;

/// <summary>
/// One entry that SHOULD be on the books, and the source that says which fact it is.
///
/// Posting is convergent: every path re-derives what ought to exist for a source and reconciles
/// it against what does. So a draft is not a request to write — it is a statement of the intended
/// state, and <see cref="AlreadySaidBy"/> is what decides whether anything needs to happen.
/// </summary>
public sealed record EntryDraft(
    string Source,
    DateTime Date,
    string Description,
    IReadOnlyList<JournalLineDraft> Lines)
{
    /// <summary>
    /// A balanced move between two accounts. Every posting in this service is one of these —
    /// incurring a cost against the payable, a member taking on their share, settling up, paying
    /// the vendor. Which two accounts and which direction is the accounting; the shape is not.
    /// </summary>
    public static EntryDraft Between(
        AccountId debit, AccountId credit, Money amount, DateTime date, string description, string source)
    {
        // Both legs on one account nets to nothing while still satisfying double-entry, so it
        // passes every check the journal makes and records an event that did not happen.
        if (debit == credit)
            throw new InvalidOperationException("A movement needs two different accounts.");

        return new EntryDraft(
            source,
            DateTime.SpecifyKind(date.Date, DateTimeKind.Utc),
            description,
            [JournalLineDraft.Debit(debit, amount), JournalLineDraft.Credit(credit, amount)]);
    }

    /// <summary>
    /// Does this entry already say exactly what the draft says?
    ///
    /// Compares the WHOLE entry — date, description, and every line by account, direction and
    /// amount. The per-path checks this replaced looked at one debit line each and let every other
    /// difference through, so an entry that had drifted in a way the check did not inspect was
    /// left standing and reported as in sync.
    ///
    /// Line order is not significant: two entries listing the same lines in a different order are
    /// the same entry, and the order is an artefact of how the draft was built.
    /// </summary>
    public bool AlreadySaidBy(IJournalEntryFacts entry)
    {
        if (entry.Date != Date || entry.Description != Description) return false;
        if (entry.Lines.Count != Lines.Count) return false;

        var remaining = Lines.ToList();
        foreach (var line in entry.Lines)
        {
            var i = remaining.FindIndex(d =>
                d.AccountId == line.AccountId
                && d.Direction == line.Direction
                && d.Amount.Amount == line.Amount.Amount
                && d.Amount.Currency == line.Amount.Currency);
            if (i < 0) return false;
            remaining.RemoveAt(i);
        }
        return remaining.Count == 0;
    }
}

/// <summary>What a draft needs to know about a posted entry to compare itself to it. Keeps the
/// comparison in the value object without it depending on the aggregate.</summary>
public interface IJournalEntryFacts
{
    DateTime Date { get; }
    string Description { get; }
    IReadOnlyList<IJournalLineFacts> Lines { get; }
}

public interface IJournalLineFacts
{
    AccountId AccountId { get; }
    EntryDirection Direction { get; }
    Money Amount { get; }
}
