using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>The DIRECTION carries the sign; the amount is always positive.</summary>
public sealed class JournalLine
{
    public JournalLineId Id { get; private set; }
    public JournalEntryId EntryId { get; private set; }
    public AccountId AccountId { get; private set; }
    public EntryDirection Direction { get; private set; }
    public Money Amount { get; private set; }

    private JournalLine() { }

    internal JournalLine(JournalLineId id, JournalEntryId entryId, AccountId accountId, EntryDirection direction, Money amount)
    {
        Id = id;
        EntryId = entryId;
        AccountId = accountId;
        Direction = direction;
        Amount = amount;
    }

    /// <summary>+amount for a debit, −amount for a credit. Used for trial-balance and account-balance sums.</summary>
    public decimal SignedAmount => Direction == EntryDirection.Debit ? Amount.Amount : -Amount.Amount;
}

/// <summary>
/// An entry cannot exist unless Σ debits == Σ credits, and is immutable once posted —
/// corrections are mirror entries referencing the original, never edits.
/// </summary>
public sealed class JournalEntry : IAggregateRoot
{
    private readonly List<JournalLine> _lines = new();
    private readonly List<DomainEvent> _domainEvents = new();

    public JournalEntryId Id { get; private set; }
    public LedgerId LedgerId { get; private set; }
    public DateTime Date { get; private set; }          // value date — when the event economically occurred
    public string Description { get; private set; } = string.Empty;
    public string? Source { get; private set; }         // originating document (expenseId, reimbursement, bank txn) — P9
    public DateTime RecordedAt { get; private set; }    // booking date — when entered into the system

    /// <summary>
    /// Whose action produced this entry. Null for one raised by a consumer with no person behind
    /// it — a group deletion cascading, say. Provenance columns say WHAT caused an entry;
    /// this says who, which is the half an audit asks for and reversal alone cannot answer.
    /// </summary>
    public Guid? PostedByUserId { get; private set; }
    public JournalEntryId? ReversalOfEntryId { get; private set; }

    // Makes "active" a DECLARED state — in effect iff neither a reversal nor itself
    // reversed — so a partial unique index can forbid a second active journalLine per source.
    public Guid? ReversedByEntryId { get; private set; }

    // Opaque to the ledger. Lets a read model attribute an entry by column instead of
    // parsing the free-text Source. Settlement entries set all of these; expense journal_lines
    // carry only SourceExpenseId.
    public Guid? SourceExpenseId { get; private set; }
    public Guid? SourceShareId { get; private set; }
    public DateTime? SourceOccurrence { get; private set; }
    public Guid? SourceMemberId { get; private set; }

    public IReadOnlyList<JournalLine> JournalLines => _lines.AsReadOnly();
    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private JournalEntry() { }

    /// <summary>
    /// A balanced movement between two accounts — the shape of every entry this service posts.
    /// Which two accounts and in which direction is the accounting; the shape is not.
    /// </summary>
    public static JournalEntry Movement(
        LedgerId ledgerId,
        AccountId debit,
        AccountId credit,
        Money amount,
        DateTime date,
        string description,
        string? source = null,
        Guid? sourceExpenseId = null,
        Guid? sourceShareId = null,
        DateTime? sourceOccurrence = null,
        Guid? sourceMemberId = null,
        Guid? postedByUserId = null)
    {
        // Both legs on one account nets to nothing while still satisfying double-entry, so it
        // passes every check below and records an event that did not happen.
        if (debit == credit)
            throw new InvalidOperationException("A movement needs two different accounts.");

        return Post(
            ledgerId, DateTime.SpecifyKind(date.Date, DateTimeKind.Utc), description,
            [JournalLineDraft.Debit(debit, amount), JournalLineDraft.Credit(credit, amount)],
            source, sourceExpenseId, sourceShareId, sourceOccurrence, sourceMemberId, postedByUserId);
    }

    /// <summary>
    /// Post a balanced journal entry. Throws unless the lines satisfy double-entry:
    /// ≥2 lines, all positive, single currency (P10), and Σ debits == Σ credits (P2).
    /// </summary>
    public static JournalEntry Post(
        LedgerId ledgerId,
        DateTime date,
        string description,
        IReadOnlyList<JournalLineDraft> lines,
        string? source = null,
        Guid? sourceExpenseId = null,
        Guid? sourceShareId = null,
        DateTime? sourceOccurrence = null,
        Guid? sourceMemberId = null,
        Guid? postedByUserId = null)
    {
        Validate(lines);

        var entry = new JournalEntry
        {
            Id = JournalEntryId.New(),
            LedgerId = ledgerId,
            Date = DateTime.SpecifyKind(date, DateTimeKind.Utc),
            Description = description,
            Source = source,
            RecordedAt = DateTime.UtcNow,
            SourceExpenseId = sourceExpenseId,
            SourceShareId = sourceShareId,
            SourceOccurrence = sourceOccurrence is null
                ? null : DateTime.SpecifyKind(sourceOccurrence.Value.Date, DateTimeKind.Utc),
            SourceMemberId = sourceMemberId,
            PostedByUserId = postedByUserId,
        };
        foreach (var l in lines)
            entry._lines.Add(new JournalLine(JournalLineId.New(), entry.Id, l.AccountId, l.Direction, l.Amount));

        entry._domainEvents.Add(new JournalEntryPosted(entry.Id, ledgerId, entry.Date, description));
        return entry;
    }

    /// <summary>
    /// A reversing entry (P4): a new, balanced entry that mirrors this one — every debit
    /// becomes a credit and vice versa — referencing the original. The original is left
    /// untouched; the pair nets to zero across every affected account.
    /// </summary>
    public JournalEntry Reverse(DateTime date, string? description = null, Guid? reversedByUserId = null)
    {
        if (ReversalOfEntryId is not null)
            throw new InvalidOperationException("Cannot reverse a reversing entry.");
        if (ReversedByEntryId is not null)
            throw new InvalidOperationException("Entry has already been reversed.");

        var mirrored = _lines
            .Select(p => new JournalLineDraft(p.AccountId, Flip(p.Direction), p.Amount))
            .ToList();

        var reversal = new JournalEntry
        {
            Id = JournalEntryId.New(),
            LedgerId = LedgerId,
            Date = DateTime.SpecifyKind(date, DateTimeKind.Utc),
            Description = description ?? $"Reversal of: {Description}",
            Source = Source,
            RecordedAt = DateTime.UtcNow,
            ReversalOfEntryId = Id,
            // Carry the provenance so a reversal is attributable to the same origin and the
            // read-side signed-sum nets the pair to zero.
            SourceExpenseId = SourceExpenseId,
            SourceShareId = SourceShareId,
            SourceOccurrence = SourceOccurrence,
            SourceMemberId = SourceMemberId,
            // The reversal is a NEW act by whoever caused it, not a copy of the original's author.
            PostedByUserId = reversedByUserId,
        };
        foreach (var l in mirrored)
            reversal._lines.Add(new JournalLine(JournalLineId.New(), reversal.Id, l.AccountId, l.Direction, l.Amount));

        reversal._domainEvents.Add(new JournalEntryReversed(reversal.Id, Id, LedgerId));

        // Mark the original reversed in the same unit of work. The original is EF-tracked in every
        // reversal flow (entries are loaded tracking), so this persists with the reversal on commit.
        ReversedByEntryId = reversal.Id.Value;
        return reversal;
    }

    /// <summary>
    /// In effect: neither a reversal itself nor already reversed. Both are declared columns, so
    /// deciding this never means scanning for reversal pairs.
    /// </summary>
    public bool IsInEffect => ReversalOfEntryId is null && ReversedByEntryId is null;

    /// <summary>
    /// Does this entry already say exactly what <paramref name="other"/> says?
    ///
    /// Compares the WHOLE entry — date, description, and every line by account, direction and
    /// amount. The per-path checks this replaced read one debit line each and let every other
    /// difference through, so an entry that had drifted in a way its check did not inspect was
    /// left standing and reported as in sync.
    ///
    /// Line order is not significant: the same lines in another order are the same entry, and the
    /// order is an artefact of how the caller assembled them.
    /// </summary>
    public bool SaysTheSameAs(JournalEntry other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.Date != Date || other.Description != Description) return false;
        if (other._lines.Count != _lines.Count) return false;

        var unmatched = _lines.ToList();
        foreach (var line in other._lines)
        {
            var i = unmatched.FindIndex(l =>
                l.AccountId == line.AccountId
                && l.Direction == line.Direction
                && l.Amount.Amount == line.Amount.Amount
                && l.Amount.Currency == line.Amount.Currency);
            if (i < 0) return false;
            unmatched.RemoveAt(i);
        }
        return true;
    }

    private static EntryDirection Flip(EntryDirection d) =>
        d == EntryDirection.Debit ? EntryDirection.Credit : EntryDirection.Debit;

    private static void Validate(IReadOnlyList<JournalLineDraft> lines)
    {
        if (lines.Count < 2)
            throw new ArgumentException("A journal entry needs at least two journal_lines (double-entry).", nameof(lines));
        if (lines.Any(l => l.Amount.Amount <= 0))
            throw new ArgumentException("JournalLine amounts must be positive — the direction carries the sign.", nameof(lines));

        var currency = lines[0].Amount.Currency;
        if (lines.Any(l => l.Amount.Currency != currency))
            throw new InvalidOperationException("All journal_lines in an entry must share one currency (P10).");

        var debits = lines.Where(l => l.Direction == EntryDirection.Debit).Sum(l => l.Amount.Amount);
        var credits = lines.Where(l => l.Direction == EntryDirection.Credit).Sum(l => l.Amount.Amount);
        if (debits != credits)
            throw new InvalidOperationException($"Entry does not balance: debits {debits} != credits {credits} (double-entry P2).");
    }
}

public static class JournalEntryExtensions
{
    /// <summary>The entries under a source that still stand — the set every convergence compares
    /// against.</summary>
    public static IReadOnlyList<JournalEntry> InEffect(this IEnumerable<JournalEntry> entries)
        => entries.Where(e => e.IsInEffect).ToList();
}
