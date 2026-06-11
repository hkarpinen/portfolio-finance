using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// One line of a journal entry against one account. Stores a true <see cref="EntryDirection"/>
/// (Debit/Credit) and a positive <see cref="Money"/> amount — the direction carries the sign,
/// matching how an accountant reads the books rather than hiding everything behind a signed number.
/// </summary>
public sealed class Posting
{
    public PostingId Id { get; private set; }
    public JournalEntryId EntryId { get; private set; }
    public AccountId AccountId { get; private set; }
    public EntryDirection Direction { get; private set; }
    public Money Amount { get; private set; }

    private Posting() { }

    internal Posting(PostingId id, JournalEntryId entryId, AccountId accountId, EntryDirection direction, Money amount)
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
/// The book of original entry: one economic event posted to one ledger as a set of
/// balanced postings. The consistency boundary for double-entry (P2) — an entry cannot
/// exist unless Σ debits == Σ credits. Immutable once posted (P4): corrections are
/// <see cref="Reverse"/> entries (mirror the debits/credits) referencing the original.
/// </summary>
public sealed class JournalEntry : IAggregateRoot
{
    private readonly List<Posting> _postings = new();
    private readonly List<DomainEvent> _domainEvents = new();

    public JournalEntryId Id { get; private set; }
    public LedgerId LedgerId { get; private set; }
    public DateTime Date { get; private set; }          // value date — when the event economically occurred
    public string Description { get; private set; } = string.Empty;
    public string? Source { get; private set; }         // originating document (expenseId, reimbursement, bank txn) — P9
    public DateTime RecordedAt { get; private set; }    // booking date — when entered into the system
    public JournalEntryId? ReversalOfEntryId { get; private set; }

    // Set on THIS entry when it is reversed (points at the reversing entry). Together with
    // ReversalOfEntryId this makes "active" a declared state — an entry is in effect iff it is
    // neither a reversal nor itself reversed — instead of a derived reversal-pair scan, and lets a
    // partial unique index forbid a second active posting under the same source.
    public Guid? ReversedByEntryId { get; private set; }

    // Structured source provenance (P9, §11.4) — nullable, opaque to the ledger. Lets read
    // models attribute an entry back to its originating (charge, allocation, occurrence, member)
    // by column rather than by parsing the free-text Source. Set on settlement entries; charge
    // postings carry only SourceChargeId.
    public Guid? SourceChargeId { get; private set; }
    public Guid? SourceAllocationId { get; private set; }
    public DateTime? SourceOccurrence { get; private set; }
    public Guid? SourceMemberId { get; private set; }

    public IReadOnlyList<Posting> Postings => _postings.AsReadOnly();
    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private JournalEntry() { }

    /// <summary>
    /// Post a balanced journal entry. Throws unless the lines satisfy double-entry:
    /// ≥2 postings, all positive, single currency (P10), and Σ debits == Σ credits (P2).
    /// </summary>
    public static JournalEntry Post(
        LedgerId ledgerId,
        DateTime date,
        string description,
        IReadOnlyList<PostingLine> lines,
        string? source = null,
        Guid? sourceChargeId = null,
        Guid? sourceAllocationId = null,
        DateTime? sourceOccurrence = null,
        Guid? sourceMemberId = null)
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
            SourceChargeId = sourceChargeId,
            SourceAllocationId = sourceAllocationId,
            SourceOccurrence = sourceOccurrence is null
                ? null : DateTime.SpecifyKind(sourceOccurrence.Value.Date, DateTimeKind.Utc),
            SourceMemberId = sourceMemberId,
        };
        foreach (var l in lines)
            entry._postings.Add(new Posting(PostingId.New(), entry.Id, l.AccountId, l.Direction, l.Amount));

        entry._domainEvents.Add(new JournalEntryPosted(entry.Id, ledgerId, entry.Date, description));
        return entry;
    }

    /// <summary>
    /// A reversing entry (P4): a new, balanced entry that mirrors this one — every debit
    /// becomes a credit and vice versa — referencing the original. The original is left
    /// untouched; the pair nets to zero across every affected account.
    /// </summary>
    public JournalEntry Reverse(DateTime date, string? description = null)
    {
        if (ReversalOfEntryId is not null)
            throw new InvalidOperationException("Cannot reverse a reversing entry.");
        if (ReversedByEntryId is not null)
            throw new InvalidOperationException("Entry has already been reversed.");

        var mirrored = _postings
            .Select(p => new PostingLine(p.AccountId, Flip(p.Direction), p.Amount))
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
            SourceChargeId = SourceChargeId,
            SourceAllocationId = SourceAllocationId,
            SourceOccurrence = SourceOccurrence,
            SourceMemberId = SourceMemberId,
        };
        foreach (var l in mirrored)
            reversal._postings.Add(new Posting(PostingId.New(), reversal.Id, l.AccountId, l.Direction, l.Amount));

        reversal._domainEvents.Add(new JournalEntryReversed(reversal.Id, Id, LedgerId));

        // Mark the original reversed in the same unit of work. The original is EF-tracked in every
        // reversal flow (entries are loaded tracking), so this persists with the reversal on commit.
        ReversedByEntryId = reversal.Id.Value;
        return reversal;
    }

    private static EntryDirection Flip(EntryDirection d) =>
        d == EntryDirection.Debit ? EntryDirection.Credit : EntryDirection.Debit;

    private static void Validate(IReadOnlyList<PostingLine> lines)
    {
        if (lines.Count < 2)
            throw new ArgumentException("A journal entry needs at least two postings (double-entry).", nameof(lines));
        if (lines.Any(l => l.Amount.Amount <= 0))
            throw new ArgumentException("Posting amounts must be positive — the direction carries the sign.", nameof(lines));

        var currency = lines[0].Amount.Currency;
        if (lines.Any(l => l.Amount.Currency != currency))
            throw new InvalidOperationException("All postings in an entry must share one currency (P10).");

        var debits = lines.Where(l => l.Direction == EntryDirection.Debit).Sum(l => l.Amount.Amount);
        var credits = lines.Where(l => l.Direction == EntryDirection.Credit).Sum(l => l.Amount.Amount);
        if (debits != credits)
            throw new InvalidOperationException($"Entry does not balance: debits {debits} != credits {credits} (double-entry P2).");
    }
}
