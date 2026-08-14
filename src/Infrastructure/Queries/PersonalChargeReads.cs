using Infrastructure.Persistence;

namespace Infrastructure.Queries;

/// <summary>
/// Whether a personal charge has been paid.
///
/// Same question, same answer as a group bill: the charge credits Payable when it is incurred and
/// debits it when it is settled, so a zero balance means paid. Both books use the same account
/// code, which is why one derivation serves both — and why no paid FLAG is needed anywhere.
///
/// Funding does not enter into it. Settling from a card or from cash both clear the payable; the
/// company was paid either way, and the funding account only records where the money came from.
/// </summary>
internal static class PersonalChargeReads
{
    public static async Task<HashSet<Guid>> GetPaidAsync(
        FinanceDbContext db, IReadOnlyCollection<Guid> chargeIds, CancellationToken ct = default)
    {
        if (chargeIds.Count == 0) return [];

        var owed = await VendorPaymentReads.GetOwedToVendorByChargeAsync(db, chargeIds, ct);

        // Absent from the map means nothing was ever accrued against it, which is not the same as
        // settled — an unposted charge is not a paid one.
        return chargeIds.Where(id => owed.TryGetValue(id, out var balance) && balance <= 0m).ToHashSet();
    }

    public static async Task<bool> IsPaidAsync(FinanceDbContext db, Guid chargeId, CancellationToken ct = default)
        => (await GetPaidAsync(db, [chargeId], ct)).Count > 0;
}
