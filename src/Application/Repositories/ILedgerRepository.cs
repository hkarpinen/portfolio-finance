using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

public interface ILedgerRepository
{
    Task<Ledger?> GetLedgerByOwnerAsync(LedgerOwnerType ownerType, Guid ownerId, CancellationToken ct = default);
    Task AddLedgerAsync(Ledger ledger, CancellationToken ct = default);

    /// <summary>
    /// This owner's book, opened and seeded from <paramref name="seed"/> if they have none.
    ///
    /// Which chart seeds it is the caller's decision — a group's book and a person's start with
    /// different accounts — but opening one and writing its first accounts together is persistence.
    /// </summary>
    Task<Ledger> GetOrOpenLedgerAsync(
        LedgerOwnerType ownerType, Guid ownerId, string currency,
        Func<LedgerId, IReadOnlyList<Account>> seed, CancellationToken ct = default);

    Task<IReadOnlyList<Account>> GetAccountsAsync(LedgerId ledgerId, CancellationToken ct = default);
    Task<Account?> GetAccountByCodeAsync(LedgerId ledgerId, string code, CancellationToken ct = default);
    Task<Account?> GetAccountAsync(AccountId accountId, CancellationToken ct = default);
    Task AddAccountAsync(Account account, CancellationToken ct = default);

    /// <summary>
    /// The account with this code, opening it from <paramref name="open"/> if the ledger has none.
    /// The factory rather than a built account, so a chart's naming is only invoked when it is
    /// actually needed.
    /// </summary>
    Task<Account> GetOrOpenAccountAsync(LedgerId ledgerId, AccountSpec spec, CancellationToken ct = default);

    Task AddDebtTermsAsync(DebtTerms terms, CancellationToken ct = default);
    Task<IReadOnlyList<DebtTerms>> GetDebtTermsForUserAsync(Guid userId, CancellationToken ct = default);

    Task AddJournalEntryAsync(JournalEntry entry, CancellationToken ct = default);
    // Returns BOTH originals and any reversals, which is what makes a posting idempotent to redo and
    // possible to unwind.
    Task<IReadOnlyList<JournalEntry>> GetEntriesBySourceAsync(LedgerId ledgerId, string source, CancellationToken ct = default);
    // Accrual, vendor payment and settlements alike.
    Task<IReadOnlyList<JournalEntry>> GetEntriesByChargeAsync(LedgerId ledgerId, Guid chargeId, CancellationToken ct = default);
    Task<IReadOnlyList<Posting>> GetPostingsByAccountAsync(AccountId accountId, CancellationToken ct = default);
    Task<IReadOnlyList<Posting>> GetPostingsByLedgerAsync(LedgerId ledgerId, CancellationToken ct = default);

    Task CommitAsync(CancellationToken ct = default);
}
