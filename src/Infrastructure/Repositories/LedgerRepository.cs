using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class LedgerRepository : ILedgerRepository
{
    private readonly FinanceDbContext _db;

    public LedgerRepository(FinanceDbContext db) => _db = db;

    public Task<Ledger?> GetLedgerByOwnerAsync(LedgerOwnerType ownerType, Guid ownerId, CancellationToken ct = default)
        => _db.Ledgers.FirstOrDefaultAsync(l => l.OwnerType == ownerType && l.OwnerId == ownerId, ct);

    public async Task AddLedgerAsync(Ledger ledger, CancellationToken ct = default)
        => await _db.Ledgers.AddAsync(ledger, ct);

    public async Task<IReadOnlyList<Account>> GetAccountsAsync(LedgerId ledgerId, CancellationToken ct = default)
        => await _db.Accounts.Where(a => a.LedgerId == ledgerId).ToListAsync(ct);

    public Task<Account?> GetAccountByCodeAsync(LedgerId ledgerId, string code, CancellationToken ct = default)
        => _db.Accounts.FirstOrDefaultAsync(a => a.LedgerId == ledgerId && a.Code == code, ct);

    public async Task AddAccountAsync(Account account, CancellationToken ct = default)
        => await _db.Accounts.AddAsync(account, ct);

    public async Task AddJournalEntryAsync(JournalEntry entry, CancellationToken ct = default)
        => await _db.JournalEntries.AddAsync(entry, ct);

    public async Task<IReadOnlyList<JournalEntry>> GetEntriesBySourceAsync(LedgerId ledgerId, string source, CancellationToken ct = default)
        => await _db.JournalEntries
            .Include("_postings")
            .Where(e => e.LedgerId == ledgerId && e.Source == source)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<JournalEntry>> GetEntriesByChargeAsync(LedgerId ledgerId, Guid chargeId, CancellationToken ct = default)
        => await _db.JournalEntries
            .Include("_postings")
            .Where(e => e.LedgerId == ledgerId && e.SourceChargeId == chargeId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Posting>> GetPostingsByAccountAsync(AccountId accountId, CancellationToken ct = default)
        => await _db.Postings.AsNoTracking().Where(p => p.AccountId == accountId).ToListAsync(ct);

    public async Task<IReadOnlyList<Posting>> GetPostingsByLedgerAsync(LedgerId ledgerId, CancellationToken ct = default)
    {
        var query =
            from p in _db.Postings.AsNoTracking()
            join e in _db.JournalEntries.AsNoTracking() on p.EntryId equals e.Id
            where e.LedgerId == ledgerId
            select p;
        return await query.ToListAsync(ct);
    }

    public Task CommitAsync(CancellationToken ct = default) => _db.SaveChangesAsync(ct);
}
