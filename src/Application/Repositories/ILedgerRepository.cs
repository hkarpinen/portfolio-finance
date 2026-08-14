using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

public interface ILedgerRepository
{
    Task<Ledger?> GetLedgerByOwnerAsync(AccountingEntity owner, CancellationToken ct = default);
    Task AddLedgerAsync(Ledger ledger, CancellationToken ct = default);

    /// <summary>
    /// This owner's book, opened and seeded from <paramref name="seed"/> if they have none.
    ///
    /// What a new book starts with comes from <see cref="Chart.StandardAccounts"/> for that
    /// entity — the caller used to pass both the kind and the matching chart, which was the same
    /// decision made twice.
    /// </summary>
    Task<Ledger> GetOrOpenLedgerAsync(
        AccountingEntity owner, string currency, CancellationToken ct = default);

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

    /// <summary>
    /// Makes the books say what <paramref name="candidate"/> says, and commits.
    ///
    /// Reads what is in effect under the candidate's source, asks <see cref="ConvergencePlan"/>
    /// what has to change, then writes the reversals and the replacement together. Decides
    /// nothing itself — the plan is a pure domain call — but reading, writing and committing as
    /// one unit is persistence, and every posting path was spelling it out for itself.
    ///
    /// Returns whether anything was written: false means the books already agreed, which is the
    /// common case for a redelivered message or an edit that changed nothing.
    /// </summary>
    /// <param name="postOnce">
    /// True for a fact rather than a statement — money changing hands. Converging one would CORRECT
    /// it if the figure upstream had moved, and a payment that happened is reversed, not restated.
    /// So anything already in effect under the source wins and nothing is written.
    /// </param>
    Task<bool> ConvergeAsync(JournalEntry candidate, bool postOnce = false, CancellationToken ct = default);
    // Returns BOTH originals and any reversals, which is what makes a journalLine idempotent to redo and
    // possible to unwind.
    Task<IReadOnlyList<JournalEntry>> GetEntriesBySourceAsync(LedgerId ledgerId, string source, CancellationToken ct = default);
    // Accrual, vendor payment and settlements alike.
    Task<IReadOnlyList<JournalEntry>> GetEntriesByExpenseAsync(LedgerId ledgerId, Guid expenseId, CancellationToken ct = default);
    Task<IReadOnlyList<JournalLine>> GetJournalLinesByAccountAsync(AccountId accountId, CancellationToken ct = default);
    Task<IReadOnlyList<JournalLine>> GetJournalLinesByLedgerAsync(LedgerId ledgerId, CancellationToken ct = default);

    Task CommitAsync(CancellationToken ct = default);
}
