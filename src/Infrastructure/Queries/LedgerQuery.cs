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
            .FirstOrDefaultAsync(l => l.OwnerType == LedgerOwnerType.Group && l.OwnerId == groupId, ct);
        if (ledger is null) return null;

        var accounts = await _db.Accounts.AsNoTracking()
            .Where(a => a.LedgerId == ledger.Id)
            .ToListAsync(ct);

        var postings = await (
            from p in _db.Postings.AsNoTracking()
            join e in _db.JournalEntries.AsNoTracking() on p.EntryId equals e.Id
            where e.LedgerId == ledger.Id
            select p).ToListAsync(ct);

        var byAccount = postings.ToLookup(p => p.AccountId);

        var accountDtos = accounts
            .Select(a => new AccountBalanceDto(
                a.Id.Value, a.Code, a.Name,
                a.AccountType.ToString(), a.NormalBalance.ToString(),
                LedgerMath.AccountBalance(a.NormalBalance, byAccount[a.Id])))
            .OrderBy(a => a.Code)
            .ToList();

        var (debits, credits) = LedgerMath.TrialBalance(postings);
        return new LedgerViewDto(ledger.Id.Value, ledger.Currency, accountDtos, debits, credits, debits == credits);
    }

    public async Task<AccountStatementDto?> GetAccountStatementAsync(Guid groupId, Guid accountId, CancellationToken ct = default)
    {
        var ledger = await _db.Ledgers.AsNoTracking()
            .FirstOrDefaultAsync(l => l.OwnerType == LedgerOwnerType.Group && l.OwnerId == groupId, ct);
        if (ledger is null) return null;

        var accId = new AccountId(accountId);
        var account = await _db.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.LedgerId == ledger.Id && a.Id == accId, ct);
        if (account is null) return null;

        // Postings on this account with their entry (date/description/source), oldest first.
        var rows = await (
            from p in _db.Postings.AsNoTracking()
            join e in _db.JournalEntries.AsNoTracking() on p.EntryId equals e.Id
            where e.LedgerId == ledger.Id && p.AccountId == accId
            orderby e.Date, e.RecordedAt
            select new { Posting = p, Entry = e }).ToListAsync(ct);

        var normal = account.NormalBalance;
        decimal running = 0m;
        var lines = new List<StatementLineDto>(rows.Count);
        foreach (var r in rows)
        {
            // Per-posting contribution to the oriented balance: a debit raises a debit-normal
            // account and lowers a credit-normal one (and vice-versa).
            var signed = r.Posting.Direction == EntryDirection.Debit
                ? r.Posting.Amount.Amount
                : -r.Posting.Amount.Amount;
            running += normal == NormalBalance.Debit ? signed : -signed;

            lines.Add(new StatementLineDto(
                r.Entry.Id.Value, r.Entry.Date, r.Entry.Description, r.Entry.Source,
                r.Posting.Direction.ToString(), r.Posting.Amount.Amount, running,
                r.Entry.ReversalOfEntryId is not null));
        }

        return new AccountStatementDto(
            account.Id.Value, account.Code, account.Name,
            account.AccountType.ToString(), normal.ToString(), ledger.Currency,
            running, lines);
    }

    public async Task<UserPositionDto> GetUserPositionAsync(Guid userId, CancellationToken ct = default)
    {
        var memberCode = GroupChart.MemberCode(userId);

        // Every Member:{userId} account across GROUP ledgers, with its postings + the group's id/currency.
        // The user's cross-group position is the reciprocal of these — recorded once here, not double-posted.
        var rows = await (
            from p in _db.Postings.AsNoTracking()
            join a in _db.Accounts.AsNoTracking() on p.AccountId equals a.Id
            join l in _db.Ledgers.AsNoTracking() on a.LedgerId equals l.Id
            where a.Code == memberCode && l.OwnerType == LedgerOwnerType.Group
            select new { Posting = p, GroupId = l.OwnerId, l.Currency, a.NormalBalance }).ToListAsync(ct);

        var groups = rows
            .GroupBy(x => new { x.GroupId, x.Currency, x.NormalBalance })
            .Select(g => new GroupPositionDto(
                g.Key.GroupId, g.Key.Currency,
                LedgerMath.AccountBalance(g.Key.NormalBalance, g.Select(x => x.Posting))))
            .OrderByDescending(g => Math.Abs(g.Net))
            .ToList();

        var total = groups.Sum(g => g.Net);
        var currency = groups.Count > 0 ? groups[0].Currency : null;
        return new UserPositionDto(total, currency, groups);
    }

    public async Task<bool> IsAllocationSettledAsync(Guid allocationId, DateTime occurrence, CancellationToken ct = default)
    {
        var settled = await SettlementReads.GetSettledByAllocationOccurrenceAsync(_db, new[] { allocationId }, ct);
        return settled.TryGetValue((allocationId, occurrence.Date), out var v) && v.Settled > 0m;
    }

    public Task<bool> IsVendorPaidAsync(Guid chargeId, CancellationToken ct = default)
        => VendorPaymentReads.IsVendorPaidAsync(_db, chargeId, ct);
}
