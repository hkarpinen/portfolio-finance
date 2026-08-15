using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.Engines;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class LedgerRepository : ILedgerRepository
{
    private readonly FinanceDbContext _db;

    public LedgerRepository(FinanceDbContext db) => _db = db;

    public Task<Ledger?> GetLedgerByOwnerAsync(AccountingEntity owner, CancellationToken ct = default)
        => _db.Ledgers.FirstOrDefaultAsync(l => l.Owner.Kind == owner.Kind && l.Owner.Id == owner.Id, ct);

    public async Task AddLedgerAsync(Ledger ledger, CancellationToken ct = default)
        => await _db.Ledgers.AddAsync(ledger, ct);

    public async Task<IReadOnlyList<Account>> GetAccountsAsync(LedgerId ledgerId, CancellationToken ct = default)
        => await _db.Accounts.Where(a => a.LedgerId == ledgerId).ToListAsync(ct);

    public Task<Account?> GetAccountByCodeAsync(LedgerId ledgerId, string code, CancellationToken ct = default)
        => _db.Accounts.FirstOrDefaultAsync(a => a.LedgerId == ledgerId && a.Code == code, ct);

    public async Task AddAccountAsync(Account account, CancellationToken ct = default)
        => await _db.Accounts.AddAsync(account, ct);

    public async Task<Ledger> GetOrOpenLedgerAsync(
        AccountingEntity owner, string currency, CancellationToken ct = default)
    {
        var existing = await _db.Ledgers.FirstOrDefaultAsync(l => l.Owner.Kind == owner.Kind && l.Owner.Id == owner.Id, ct);
        if (existing is not null) return existing;

        var ledger = Ledger.Open(owner, currency);
        await _db.Ledgers.AddAsync(ledger, ct);
        await _db.Accounts.AddRangeAsync(Chart.StandardAccounts(ledger.Id), ct);
        await _db.SaveChangesAsync(ct);
        return ledger;
    }

    public async Task<Account> GetOrOpenAccountAsync(
        LedgerId ledgerId, AccountSpec spec, CancellationToken ct = default)
    {
        var existing = await _db.Accounts.FirstOrDefaultAsync(a => a.LedgerId == ledgerId && a.Code == spec.Code, ct);
        if (existing is not null) return existing;

        var account = spec.Open();
        await _db.Accounts.AddAsync(account, ct);
        await _db.SaveChangesAsync(ct);
        return account;
    }

    public Task<Account?> GetAccountAsync(AccountId accountId, CancellationToken ct = default)
        => _db.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, ct);

    public async Task AddDebtTermsAsync(DebtTerms terms, CancellationToken ct = default)
        => await _db.DebtTerms.AddAsync(terms, ct);

    /// <summary>Whose debt it is comes from the ledger the account sits in — the terms hold no
    /// second copy of it.</summary>
    public async Task<IReadOnlyList<DebtTerms>> GetDebtTermsForUserAsync(Guid userId, CancellationToken ct = default)
        => await _db.DebtTerms.AsNoTracking()
            .Where(t => _db.Accounts.Any(a => a.Id == t.AccountId
                && _db.Ledgers.Any(l => l.Id == a.LedgerId
                    && l.Owner.Kind == EntityKind.Person && l.Owner.Id == userId)))
            .ToListAsync(ct);

    public async Task AddJournalEntryAsync(JournalEntry entry, CancellationToken ct = default)
        => await _db.JournalEntries.AddAsync(entry, ct);

    public async Task<bool> ConvergeAsync(JournalEntry candidate, bool postOnce = false, CancellationToken ct = default)
    {
        var inEffect = (await GetEntriesBySourceAsync(candidate.LedgerId, candidate.Source!, ct)).InEffect();
        if (postOnce && inEffect.Count > 0) return false;

        var plan = ConvergencePlan.For(candidate, inEffect);
        if (plan.AlreadyThere) return false;

        // Reversed at the ORIGINAL entry's date, not today's. A correction and its replacement have
        // to land in the same period or that period is misstated: reversing a July entry in August
        // leaves July carrying the old figure and the new one, and a balance drawn at 31 July
        // reports their sum. The cumulative position comes out right either way, which is what
        // makes that bug so quiet.
        foreach (var stale in plan.Reverse)
            await _db.JournalEntries.AddAsync(stale.Reverse(stale.Date), ct);

        if (plan.Post is { } entry)
            await _db.JournalEntries.AddAsync(entry, ct);

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // Include by the FIELD name. It is a string, so a rename of the backing field leaves this
    // compiling and throws at runtime — and this read runs before every convergence, so getting it
    // wrong stops the books being written at all, not just read.
    public async Task<IReadOnlyList<JournalEntry>> GetEntriesBySourceAsync(LedgerId ledgerId, string source, CancellationToken ct = default)
        => await _db.JournalEntries
            .Include("_lines")
            .Where(e => e.LedgerId == ledgerId && e.Source == source)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<JournalEntry>> GetEntriesByExpenseAsync(LedgerId ledgerId, Guid expenseId, CancellationToken ct = default)
        => await _db.JournalEntries
            .Include("_lines")
            .Where(e => e.LedgerId == ledgerId && e.SourceExpenseId == expenseId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<JournalLine>> GetJournalLinesByAccountAsync(AccountId accountId, CancellationToken ct = default)
        => await _db.JournalLines.AsNoTracking().Where(p => p.AccountId == accountId).ToListAsync(ct);

    public async Task<IReadOnlyList<JournalLine>> GetJournalLinesByLedgerAsync(LedgerId ledgerId, CancellationToken ct = default)
    {
        var query =
            from p in _db.JournalLines.AsNoTracking()
            join e in _db.JournalEntries.AsNoTracking() on p.EntryId equals e.Id
            where e.LedgerId == ledgerId
            select p;
        return await query.ToListAsync(ct);
    }

    public Task CommitAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
