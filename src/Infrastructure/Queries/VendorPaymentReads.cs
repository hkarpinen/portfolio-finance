using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

// Derives "how much is still owed to the vendor, per charge" from the Vendor Payable account —
// the ledger is the single source of truth and there is no VendorPayment table. The accrual
// create entry credits Vendor Payable (we owe); the mark-paid transfer debits it (cleared); a
// reversal copies the SourceChargeId and flips direction, so the signed sum nets a reversed pair
// to zero. Owed balance 0 means vendor-paid — which also makes legacy cash-basis charges, which
// never touched the account, read as paid with no backfill.
internal static class VendorPaymentReads
{
    // A charge absent from the map owes 0 (already paid, or never accrued).
    public static async Task<Dictionary<Guid, decimal>> GetOwedToVendorByChargeAsync(
        FinanceDbContext db, IReadOnlyCollection<Guid> chargeIds, CancellationToken ct)
    {
        if (chargeIds.Count == 0) return new();

        var rows = await (
            from e in db.JournalEntries.AsNoTracking()
            where e.SourceChargeId != null && chargeIds.Contains(e.SourceChargeId.Value)
            join p in db.Postings.AsNoTracking() on e.Id equals p.EntryId
            join a in db.Accounts.AsNoTracking() on p.AccountId equals a.Id
            where a.Code == GroupChart.VendorPayableCode
            select new { ChargeId = e.SourceChargeId!.Value, p.Direction, Amount = p.Amount.Amount })
            .ToListAsync(ct);

        // Vendor Payable is credit-normal: owed = Σ credits − Σ debits.
        return rows
            .GroupBy(x => x.ChargeId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.Direction == EntryDirection.Credit ? x.Amount : -x.Amount));
    }

    public static async Task<bool> IsVendorPaidAsync(FinanceDbContext db, Guid chargeId, CancellationToken ct)
    {
        var owed = await GetOwedToVendorByChargeAsync(db, new[] { chargeId }, ct);
        return !owed.TryGetValue(chargeId, out var v) || v <= 0.005m;
    }
}
