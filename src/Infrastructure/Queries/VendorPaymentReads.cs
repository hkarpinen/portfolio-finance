using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

/// <summary>
/// Derives "how much is still owed to the vendor, per charge" from the Vendor Payable account
/// (<see cref="GroupChart.VendorPayableCode"/>) — the ledger is the single source of truth (there is
/// no VendorPayment table). The accrual create entry credits Vendor Payable (we owe); the mark-paid
/// transfer debits it (cleared); a reversal copies the <c>SourceChargeId</c> and flips direction, so
/// the signed sum nets a reversed pair to zero. A charge is "vendor paid" when its owed balance is 0
/// — including legacy cash-basis charges, which never touched Vendor Payable and so read as paid with
/// no backfill.
/// </summary>
internal static class VendorPaymentReads
{
    /// <summary>Owed-to-vendor amount per charge id (credits − debits on Vendor Payable, attributed
    /// by <c>SourceChargeId</c>). A charge absent from the map owes 0 (already paid / never accrued).</summary>
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

    /// <summary>True when the charge owes nothing to the vendor (paid, or legacy cash-basis).</summary>
    public static async Task<bool> IsVendorPaidAsync(FinanceDbContext db, Guid chargeId, CancellationToken ct)
    {
        var owed = await GetOwedToVendorByChargeAsync(db, new[] { chargeId }, ct);
        return !owed.TryGetValue(chargeId, out var v) || v <= 0.005m;
    }
}
