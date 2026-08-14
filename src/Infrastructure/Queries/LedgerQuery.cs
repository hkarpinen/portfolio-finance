using Finance.Application.Queries;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class LedgerQuery : ILedgerQuery
{
    private readonly FinanceDbContext _db;

    public LedgerQuery(FinanceDbContext db) => _db = db;

    public async Task<LedgerViewDto?> GetGroupLedgerAsync(Guid groupId, CancellationToken ct = default)
    {
        var ledger = await _db.Ledgers.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Owner.Kind == EntityKind.Household && l.Owner.Id == groupId, ct);
        if (ledger is null) return null;

        var accounts = await _db.Accounts.AsNoTracking()
            .Where(a => a.LedgerId == ledger.Id)
            .ToListAsync(ct);

        var lines = await (
            from p in _db.JournalLines.AsNoTracking()
            join e in _db.JournalEntries.AsNoTracking() on p.EntryId equals e.Id
            where e.LedgerId == ledger.Id
            select p).ToListAsync(ct);

        var byAccount = lines.ToLookup(p => p.AccountId);

        var accountDtos = accounts
            .Select(a => new AccountBalanceDto(
                a.Id.Value, a.Code, a.Name,
                a.AccountType.ToString(), a.NormalBalance.ToString(),
                a.BalanceFrom(byAccount[a.Id])))
            .OrderBy(a => a.Code)
            .ToList();

        var (debits, credits) = LedgerMath.TrialBalance(lines);
        return new LedgerViewDto(ledger.Id.Value, ledger.Currency, accountDtos, debits, credits, debits == credits);
    }

    public async Task<AccountStatementDto?> GetAccountStatementAsync(Guid groupId, Guid accountId, CancellationToken ct = default)
    {
        var ledger = await _db.Ledgers.AsNoTracking()
            .FirstOrDefaultAsync(l => l.Owner.Kind == EntityKind.Household && l.Owner.Id == groupId, ct);
        if (ledger is null) return null;

        var accId = new AccountId(accountId);
        var account = await _db.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.LedgerId == ledger.Id && a.Id == accId, ct);
        if (account is null) return null;

        // Oldest first — the running balance below depends on it.
        var rows = await (
            from p in _db.JournalLines.AsNoTracking()
            join e in _db.JournalEntries.AsNoTracking() on p.EntryId equals e.Id
            where e.LedgerId == ledger.Id && p.AccountId == accId
            orderby e.Date, e.RecordedAt
            select new { JournalLine = p, Entry = e }).ToListAsync(ct);

        var normal = account.NormalBalance;
        decimal running = 0m;
        var lines = new List<StatementLineDto>(rows.Count);
        foreach (var r in rows)
        {
            // Per-journalLine contribution to the oriented balance: a debit raises a debit-normal account and
            // lowers a credit-normal one, and vice-versa.
            var signed = r.JournalLine.Direction == EntryDirection.Debit
                ? r.JournalLine.Amount.Amount
                : -r.JournalLine.Amount.Amount;
            running += normal == NormalBalance.Debit ? signed : -signed;

            lines.Add(new StatementLineDto(
                r.Entry.Id.Value, r.Entry.Date, r.Entry.Description, r.Entry.Source,
                r.JournalLine.Direction.ToString(), r.JournalLine.Amount.Amount, running,
                r.Entry.ReversalOfEntryId is not null));
        }

        return new AccountStatementDto(
            account.Id.Value, account.Code, account.Name,
            account.AccountType.ToString(), normal.ToString(), ledger.Currency,
            running, lines);
    }

    public async Task<UserPositionDto> GetUserPositionAsync(Guid userId, CancellationToken ct = default)
    {
        var memberCode = Chart.MemberCode(userId);

        // The user's cross-group position is the RECIPROCAL of these member-equity balances — recorded
        // once, in the group ledger, never double-posted into a second book.
        var rows = await (
            from p in _db.JournalLines.AsNoTracking()
            join a in _db.Accounts.AsNoTracking() on p.AccountId equals a.Id
            join l in _db.Ledgers.AsNoTracking() on a.LedgerId equals l.Id
            where a.Code == memberCode && l.Owner.Kind == EntityKind.Household
            select new { JournalLine = p, GroupId = l.Owner.Id, l.Currency, a.NormalBalance }).ToListAsync(ct);

        var groups = rows
            .GroupBy(x => new { x.GroupId, x.Currency, x.NormalBalance })
            .Select(g => new GroupPositionDto(
                g.Key.GroupId, g.Key.Currency,
                LedgerMath.AccountBalance(g.Key.NormalBalance, g.Select(x => x.JournalLine))))
            .OrderByDescending(g => Math.Abs(g.Net))
            .ToList();

        var total = groups.Sum(g => g.Net);
        var currency = groups.Count > 0 ? groups[0].Currency : null;
        return new UserPositionDto(total, currency, groups);
    }

    public async Task<bool> IsShareSettledAsync(Guid shareId, DateTime occurrence, CancellationToken ct = default)
    {
        var settled = await SettlementReads.GetSettledByShareOccurrenceAsync(_db, new[] { shareId }, ct);
        return settled.TryGetValue((shareId, occurrence.Date), out var v) && v.Settled > 0m;
    }

    public Task<bool> IsVendorPaidAsync(Guid expenseId, CancellationToken ct = default)
        => VendorPaymentReads.IsVendorPaidAsync(_db, expenseId, ct);
}
