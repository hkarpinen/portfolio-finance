using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

// Derives "settled per (share, occurrence)" from settlement journal entries — the ledger is
// the single source of truth and there is no reimbursements table. A settlement entry carries its
// SourceShareId/SourceOccurrence provenance and two equal journal lines; a reversal copies the
// provenance and negates, so the signed sum nets a reversed pair to zero.
internal static class SettlementReads
{
    public static async Task<Dictionary<(Guid ShareId, DateTime Occurrence), (decimal Settled, DateTime LatestValueDate)>>
        GetSettledByShareOccurrenceAsync(
            FinanceDbContext db, IReadOnlyCollection<Guid> shareIds, CancellationToken ct)
    {
        if (shareIds.Count == 0) return new();

        var rows = await db.JournalEntries
            .AsNoTracking()
            .Where(e => e.SourceShareId != null
                     && e.SourceOccurrence != null
                     && shareIds.Contains(e.SourceShareId.Value))
            .Select(e => new
            {
                ShareId = e.SourceShareId!.Value,
                Occurrence = e.SourceOccurrence!.Value,
                ValueDate = e.Date,
                IsReversal = e.ReversalOfEntryId != null,
                // Both journal lines of a settlement carry the same amount; either one gives the value.
                Amount = db.JournalLines.Where(p => p.EntryId == e.Id).Max(p => p.Amount.Amount),
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(x => (x.ShareId, x.Occurrence.Date))
            .ToDictionary(
                g => g.Key,
                g => (Settled: g.Sum(x => x.IsReversal ? -x.Amount : x.Amount),
                      LatestValueDate: g.Max(x => x.ValueDate)));
    }
}
