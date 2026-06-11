using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

/// <summary>
/// Derives "settled per (allocation, occurrence)" from settlement journal entries — the ledger
/// is the single source of truth (there is no reimbursements table). A settlement entry carries
/// its <c>SourceAllocationId</c>/<c>SourceOccurrence</c> provenance and two equal postings; a
/// reversal copies the provenance and negates, so the signed sum nets a reversed pair to zero —
/// the same semantics the old reimbursement signed-sum had.
/// </summary>
internal static class SettlementReads
{
    public static async Task<Dictionary<(Guid AllocationId, DateTime Occurrence), (decimal Settled, DateTime LatestValueDate)>>
        GetSettledByAllocationOccurrenceAsync(
            FinanceDbContext db, IReadOnlyCollection<Guid> allocationIds, CancellationToken ct)
    {
        if (allocationIds.Count == 0) return new();

        var rows = await db.JournalEntries
            .AsNoTracking()
            .Where(e => e.SourceAllocationId != null
                     && e.SourceOccurrence != null
                     && allocationIds.Contains(e.SourceAllocationId.Value))
            .Select(e => new
            {
                AllocationId = e.SourceAllocationId!.Value,
                Occurrence = e.SourceOccurrence!.Value,
                ValueDate = e.Date,
                IsReversal = e.ReversalOfEntryId != null,
                // Both postings of a settlement carry the same amount; either gives the value.
                Amount = db.Postings.Where(p => p.EntryId == e.Id).Max(p => p.Amount.Amount),
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(x => (x.AllocationId, x.Occurrence.Date))
            .ToDictionary(
                g => g.Key,
                g => (Settled: g.Sum(x => x.IsReversal ? -x.Amount : x.Amount),
                      LatestValueDate: g.Max(x => x.ValueDate)));
    }
}
