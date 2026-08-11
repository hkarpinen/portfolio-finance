using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

public interface ILedgerRepository
{
    Task<Ledger?> GetLedgerByOwnerAsync(LedgerOwnerType ownerType, Guid ownerId, CancellationToken ct = default);
    Task AddLedgerAsync(Ledger ledger, CancellationToken ct = default);

    Task<IReadOnlyList<Account>> GetAccountsAsync(LedgerId ledgerId, CancellationToken ct = default);
    Task<Account?> GetAccountByCodeAsync(LedgerId ledgerId, string code, CancellationToken ct = default);
    Task AddAccountAsync(Account account, CancellationToken ct = default);

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
