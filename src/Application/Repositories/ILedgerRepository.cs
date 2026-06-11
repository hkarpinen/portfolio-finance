using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

/// <summary>
/// The ledger context's store — ledgers, their chart of accounts, journal entries and
/// postings always move together, so one cohesive repository covers them. Posting reads
/// (for balances) go through the postings query surface; writes go through journal entries.
/// </summary>
public interface ILedgerRepository
{
    // Ledgers
    Task<Ledger?> GetLedgerByOwnerAsync(LedgerOwnerType ownerType, Guid ownerId, CancellationToken ct = default);
    Task AddLedgerAsync(Ledger ledger, CancellationToken ct = default);

    // Accounts (chart)
    Task<IReadOnlyList<Account>> GetAccountsAsync(LedgerId ledgerId, CancellationToken ct = default);
    Task<Account?> GetAccountByCodeAsync(LedgerId ledgerId, string code, CancellationToken ct = default);
    Task AddAccountAsync(Account account, CancellationToken ct = default);

    // Journal entries + postings
    Task AddJournalEntryAsync(JournalEntry entry, CancellationToken ct = default);
    /// <summary>Entries (with their postings) for a source document — used to make a posting
    /// idempotent and to reverse it. Returns both originals and any reversals.</summary>
    Task<IReadOnlyList<JournalEntry>> GetEntriesBySourceAsync(LedgerId ledgerId, string source, CancellationToken ct = default);
    /// <summary>All entries (with postings) tagged with a charge — accrual, vendor payment and
    /// settlements alike. Used to unwind a charge from the books when it's deactivated/deleted.</summary>
    Task<IReadOnlyList<JournalEntry>> GetEntriesByChargeAsync(LedgerId ledgerId, Guid chargeId, CancellationToken ct = default);
    Task<IReadOnlyList<Posting>> GetPostingsByAccountAsync(AccountId accountId, CancellationToken ct = default);
    Task<IReadOnlyList<Posting>> GetPostingsByLedgerAsync(LedgerId ledgerId, CancellationToken ct = default);

    Task CommitAsync(CancellationToken ct = default);
}
