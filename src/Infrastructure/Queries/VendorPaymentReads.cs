using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

// Derives "how much is still owed to the vendor, per expense" from the Vendor Payable account —
// the ledger is the single source of truth and there is no VendorPayment table. The accrual
// create entry credits Vendor Payable (we owe); the mark-paid transfer debits it (cleared); a
// reversal copies the SourceExpenseId and flips direction, so the signed sum nets a reversed pair
// to zero. Owed balance 0 means vendor-paid — which also makes legacy cash-basis expenses, which
// never touched the account, read as paid with no backfill.
internal static class VendorPaymentReads
{
    // A expense absent from the map owes 0 (already paid, or never accrued).
    public static async Task<Dictionary<Guid, decimal>> GetOwedToVendorByExpenseAsync(
        FinanceDbContext db, IReadOnlyCollection<Guid> expenseIds, CancellationToken ct)
    {
        if (expenseIds.Count == 0) return new();

        var rows = await (
            from e in db.JournalEntries.AsNoTracking()
            where e.SourceExpenseId != null && expenseIds.Contains(e.SourceExpenseId.Value)
            join p in db.JournalLines.AsNoTracking() on e.Id equals p.EntryId
            join a in db.Accounts.AsNoTracking() on p.AccountId equals a.Id
            where a.Code == Chart.PayableCode
            select new { ExpenseId = e.SourceExpenseId!.Value, p.Direction, Amount = p.Amount.Amount })
            .ToListAsync(ct);

        // Vendor Payable is credit-normal: owed = Σ credits − Σ debits.
        return rows
            .GroupBy(x => x.ExpenseId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.Direction == EntryDirection.Credit ? x.Amount : -x.Amount));
    }

    public static async Task<bool> IsVendorPaidAsync(FinanceDbContext db, Guid expenseId, CancellationToken ct)
    {
        var owed = await GetOwedToVendorByExpenseAsync(db, new[] { expenseId }, ct);
        return !owed.TryGetValue(expenseId, out var v) || v <= 0.005m;
    }
}
