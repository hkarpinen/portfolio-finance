using Finance.Application.Dtos;
using Finance.Application.Queries;
using Finance.Application.Mappers;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class ChargeQuery : IChargeQuery
{
    private readonly FinanceDbContext _db;

    public ChargeQuery(FinanceDbContext db) => _db = db;

    // ── Personal charge queries ──────────────────────────────────────────────

    public async Task<ChargeListDto> ListByUserAsync(ListChargesParams request, CancellationToken cancellationToken = default)
    {
        var userId = UserId.Create(request.UserId);
        var query = _db.Charges.Where(b => b.UserId == userId && b.GroupId == null);
        if (request.ActiveOnly) query = query.Where(b => b.IsActive);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(b => b.DueDate)
            .ThenBy(b => b.Title)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            return new ChargeListDto([], total);

        var expenseIds = items.Select(b => b.Id).ToList();
        var occurrencesByChargeId = items.ToDictionary(
            b => b.Id,
            b => b.RecurrenceSchedule?.CurrentOccurrence(b.DueDate) ?? b.DueDate);

        var payments = await _db.ChargePayments
            .AsNoTracking()
            .Where(p => expenseIds.Contains(p.ChargeId))
            .ToListAsync(cancellationToken);

        var paidChargeIds = payments
            .Where(p => occurrencesByChargeId.TryGetValue(p.ChargeId, out var occ)
                        && p.OccurrenceDate.Date == occ.Date)
            .Select(p => p.ChargeId)
            .ToHashSet();

        var responses = items
            .Select(b => ChargeMapper.ToResponse(b, paidChargeIds.Contains(b.Id)))
            .ToArray();

        return new ChargeListDto(responses, total);
    }

    public async Task<ChargeResponseDto?> GetDetailAsync(ChargeDetailParams request, CancellationToken cancellationToken = default)
    {
        var id = ChargeId.Create(request.ChargeId);
        var expense = await _db.Charges.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && b.GroupId == null, cancellationToken);
        if (expense is null) return null;

        var occurrenceDate = expense.RecurrenceSchedule?.CurrentOccurrence(expense.DueDate) ?? expense.DueDate;
        var payment = await _db.ChargePayments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.ChargeId == id && p.OccurrenceDate == occurrenceDate, cancellationToken);

        return ChargeMapper.ToResponse(expense, payment is not null);
    }

    // ── Group charge queries ─────────────────────────────────────────────

    public async Task<GroupChargeListDto> ListByGroupAsync(ListGroupChargesParams request, CancellationToken cancellationToken = default)
    {
        var groupId = GroupId.Create(request.GroupId);
        var query = _db.Charges.Where(b => b.GroupId == groupId);
        if (request.ActiveOnly) query = query.Where(b => b.IsActive);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(b => b.DueDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            return new GroupChargeListDto([], total);

        // Vendor-paid per charge — derived from the Vendor Payable balance (ledger is source of truth).
        var owedToVendor = await VendorPaymentReads.GetOwedToVendorByChargeAsync(
            _db, items.Select(b => b.Id.Value).ToList(), cancellationToken);
        bool VendorPaidFor(Guid chargeId) =>
            !owedToVendor.TryGetValue(chargeId, out var owed) || owed <= 0.005m;

        HashSet<Guid> paidChargeIds = [];
        // The caller's own share amount per charge (their allocation) — drives "your share" on the
        // client, which must reflect the real (possibly uneven) split, not an even-split estimate.
        Dictionary<Guid, decimal> callerShareByCharge = [];
        if (request.CallerId.HasValue)
        {
            var callerUserId = UserId.Create(request.CallerId.Value);
            var expenseIds = items.Select(b => b.Id).ToList();
            var callerAllocations = await _db.Allocations
                .AsNoTracking()
                .Where(s => s.UserId == callerUserId && expenseIds.Contains(s.ChargeId))
                .ToListAsync(cancellationToken);

            callerShareByCharge = callerAllocations
                .GroupBy(s => s.ChargeId.Value)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.Amount.Amount));

            if (callerAllocations.Count > 0)
            {
                var splitIds = callerAllocations.Select(s => s.Id.Value).ToList();
                var settledMap = await SettlementReads.GetSettledByAllocationOccurrenceAsync(
                    _db, splitIds, cancellationToken);

                foreach (var expense in items)
                {
                    var occurrence = expense.RecurrenceSchedule?.CurrentOccurrence(expense.DueDate) ?? expense.DueDate;
                    var split = callerAllocations.FirstOrDefault(s => s.ChargeId == expense.Id);
                    if (split is null) continue;

                    // Caller is the payer ⇒ their share is covered by paying the bill.
                    // Otherwise it's paid when settlements cover the share (signed sum from
                    // the ledger: reversals net their originals to zero).
                    // The payer's own share is covered by paying the bill (PayerUserId is
                    // always set for group charges).
                    // Payer's share is covered only once the vendor is actually paid; a settled
                    // share implies the vendor was paid (settlement is gated on it).
                    var callerIsPayer = expense.PayerUserId == callerUserId.Value;
                    var settled = settledMap.TryGetValue((split.Id.Value, occurrence.Date), out var sv) ? sv.Settled : 0m;
                    if ((callerIsPayer && VendorPaidFor(expense.Id.Value)) || settled >= split.Amount.Amount)
                        paidChargeIds.Add(expense.Id.Value);
                }
            }
        }

        var responses = items.Select(b =>
        {
            var occurrence = b.RecurrenceSchedule?.CurrentOccurrence(b.DueDate) ?? b.DueDate;
            var isPaid = paidChargeIds.Contains(b.Id.Value);
            var callerShare = callerShareByCharge.TryGetValue(b.Id.Value, out var cs) ? (decimal?)cs : null;
            return ChargeMapper.ToResponse(b, isPaid, VendorPaidFor(b.Id.Value))
                with { CurrentOccurrenceDate = occurrence, CallerShare = callerShare };
        }).ToArray();

        return new GroupChargeListDto(responses, total);
    }

    public async Task<ChargeResponseDto?> GetGroupDetailAsync(GroupChargeDetailParams request, CancellationToken cancellationToken = default)
    {
        var id = ChargeId.Create(request.ChargeId);
        var expense = await _db.Charges.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && b.GroupId != null, cancellationToken);
        if (expense is null) return null;

        var owed = await VendorPaymentReads.GetOwedToVendorByChargeAsync(_db, new[] { id.Value }, cancellationToken);
        var vendorPaid = !owed.TryGetValue(id.Value, out var v) || v <= 0.005m;
        return ChargeMapper.ToResponse(expense, vendorPaid: vendorPaid);
    }

    public async Task<IReadOnlyCollection<AllocationDto>> ListAllocationsAsync(ListAllocationsParams request, CancellationToken cancellationToken = default)
    {
        var expenseId = ChargeId.Create(request.ChargeId);
        var splits = await _db.Allocations
            .AsNoTracking()
            .Where(s => s.ChargeId == expenseId)
            .ToListAsync(cancellationToken);

        return splits.Select(ChargeMapper.ToAllocationResponse).ToArray();
    }

    public async Task<GroupChargeDetailDto?> GetGroupChargeDetailAsync(Guid expenseId, Guid callerId, CancellationToken cancellationToken = default)
    {
        var expense = await GetGroupDetailAsync(new GroupChargeDetailParams(expenseId), cancellationToken);
        if (expense is null) return null;

        var splits = await ListAllocationsAsync(new ListAllocationsParams(expenseId), cancellationToken);

        var occurrenceDate = expense.CurrentOccurrenceDate ?? expense.DueDate;
        var paidAllocationIds = await GetPaidAllocationIdsForChargeAsync(expenseId, occurrenceDate, cancellationToken);
        // The payer's own share is covered by paying the bill.
        var payerGuid = expense.PayerUserId;

        var splitUserIdSet = splits.Select(s => s.UserId).ToHashSet();
        var projections2 = (await _db.UserProjections.AsNoTracking()
            .ToListAsync(cancellationToken))
            .Where(p => splitUserIdSet.Contains(p.UserId.Value))
            .ToDictionary(p => p.UserId.Value);

        // Real roles from the membership projection (synced from household's member events);
        // "Member" is the fallback for rows that predate the projection.
        var roles = expense.GroupId is { } gid
            ? await _db.GroupMemberProjections.AsNoTracking()
                .Where(m => m.GroupId == gid)
                .ToDictionaryAsync(m => m.UserId, m => m.Role, cancellationToken)
            : new Dictionary<Guid, string>();

        var enrichedAllocations = splits.Select(s =>
        {
            projections2.TryGetValue(s.UserId, out var proj);
            return new AllocationDetailDto(
                s.AllocationId,
                s.UserId,
                proj?.GetFullName(),
                null,
                roles.GetValueOrDefault(s.UserId, "Member"),
                s.Amount,
                s.Currency,
                paidAllocationIds.Contains(s.AllocationId) || (s.UserId == payerGuid && expense.VendorPaid));
        }).ToList();

        return new GroupChargeDetailDto(expense, enrichedAllocations);
    }

    public async Task<AllocationDetailDto?> GetAllocationDetailAsync(Guid splitId, CancellationToken cancellationToken = default)
    {
        var id = AllocationId.Create(splitId);
        var split = await _db.Allocations
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (split is null) return null;

        var expense = await _db.Charges
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == split.ChargeId, cancellationToken);
        if (expense is null) return null;

        var occurrenceDate = expense.RecurrenceSchedule?.CurrentOccurrence(expense.DueDate) ?? expense.DueDate;
        var paidAllocationIds = await GetPaidAllocationIdsForChargeAsync(split.ChargeId.Value, occurrenceDate, cancellationToken);

        var projection = await _db.UserProjections
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == split.UserId, cancellationToken);

        var membershipRole = expense.GroupId is { } groupId
            ? await _db.GroupMemberProjections.AsNoTracking()
                .Where(m => m.GroupId == groupId.Value && m.UserId == split.UserId.Value)
                .Select(m => m.Role)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new AllocationDetailDto(
            split.Id.Value,
            split.UserId.Value,
            projection?.GetFullName(),
            projection?.AvatarUrl,
            membershipRole ?? "Member",
            split.Amount.Amount,
            split.Amount.Currency,
            paidAllocationIds.Contains(split.Id.Value));
    }

    public Task<bool> ExistsForUserAsync(UserId userId, string title, decimal amount, CancellationToken cancellationToken = default)
        => _db.Charges.AsNoTracking()
            .AnyAsync(
                e => e.UserId == userId && e.IsActive && e.Title == title && e.Amount.Amount == amount,
                cancellationToken);

    // ── Charge-split queries ─────────────────────────────────────────────────

    public async Task<IReadOnlyCollection<GroupMonthlyContributionsDto>> ListAllocationsByGroupAsync(
        GroupId groupId, DateTime windowStart, DateTime windowEnd, CancellationToken cancellationToken = default)
    {
        var allCharges = await _db.Charges
            .AsNoTracking()
            .Where(b => b.GroupId == groupId && b.IsActive)
            .ToListAsync(cancellationToken);

        var relevantCharges = allCharges.Where(b =>
            b.RecurrenceSchedule == null
                ? b.DueDate >= windowStart && b.DueDate <= windowEnd
                : b.RecurrenceSchedule.StartDate <= windowEnd &&
                  (b.RecurrenceSchedule.EndDate == null || b.RecurrenceSchedule.EndDate >= windowStart)
        ).ToList();

        if (relevantCharges.Count == 0) return BuildEmptyMonths(windowStart, windowEnd);

        var expenseIds = relevantCharges.Select(b => b.Id).ToList();

        var splits = await _db.Allocations
            .AsNoTracking()
            .Where(s => expenseIds.Contains(s.ChargeId))
            .ToListAsync(cancellationToken);

        if (splits.Count == 0) return BuildEmptyMonths(windowStart, windowEnd);

        var expenseById = relevantCharges.ToDictionary(b => b.Id);
        var splitUserIds = splits.Select(s => s.UserId).Distinct().ToList();

        var userProjections = await _db.UserProjections
            .AsNoTracking()
            .Where(p => splitUserIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);

        var nameById = userProjections.ToDictionary(p => p.UserId.Value, p => p.GetFullName());

        var splitIds = splits.Select(s => s.Id.Value).ToList();

        // Settled total per (allocation, occurrence) from the ledger — signed so reversals net to
        // zero. A share is "paid" when settled ≥ share (partial-aware).
        var settledByAllocationOccurrence = await SettlementReads.GetSettledByAllocationOccurrenceAsync(
            _db, splitIds, cancellationToken);

        // Vendor-paid per charge — the payer's own share counts as covered only once the bill itself
        // is paid (derived from the Vendor Payable balance — the ledger is the source of truth).
        var owedToVendor = await VendorPaymentReads.GetOwedToVendorByChargeAsync(
            _db, expenseIds.Select(e => e.Value).ToList(), cancellationToken);
        bool vendorPaidFor(Guid chargeId) =>
            !owedToVendor.TryGetValue(chargeId, out var owed) || owed <= 0.005m;

        var windowEndExclusive = windowEnd.AddDays(1);

        var projected = new List<(int Year, int Month, Guid UserId, bool IsPaid, decimal Amount, string Currency, ContributionItemDto Item)>();

        foreach (var split in splits)
        {
            if (!expenseById.TryGetValue(split.ChargeId, out var expense)) continue;

            IEnumerable<DateTime> occurrenceDates = expense.RecurrenceSchedule != null
                ? expense.RecurrenceSchedule.GetOccurrencesInRange(windowStart, windowEndExclusive)
                : [expense.DueDate];

            // The payer's own share is covered only once the vendor is actually paid (derived from
            // the Vendor Payable balance). Before that the bill is upcoming and no share is settled.
            var isPayerOwnShare = expense.PayerUserId == split.UserId.Value && vendorPaidFor(expense.Id.Value);

            foreach (var date in occurrenceDates)
            {
                var settled = settledByAllocationOccurrence.TryGetValue((split.Id.Value, date.Date), out var sv) ? sv.Settled : 0m;
                var isPaid = isPayerOwnShare || settled >= split.Amount.Amount;
                projected.Add((date.Year, date.Month, split.UserId.Value, isPaid,
                    split.Amount.Amount, split.Amount.Currency,
                    new ContributionItemDto(
                        split.Id.Value, expense.Id.Value,
                        expense.Title, expense.Category.ToString(),
                        split.Amount.Amount, split.Amount.Currency,
                        date, isPaid)));
            }
        }

        var monthCount = ((windowEnd.Year * 12 + windowEnd.Month) - (windowStart.Year * 12 + windowStart.Month)) + 1;
        var result = new List<GroupMonthlyContributionsDto>(monthCount);

        for (var m = 0; m < monthCount; m++)
        {
            var mStart = windowStart.AddMonths(m);
            var label = mStart.ToString("MMMM yyyy");
            var currency = "USD";

            var monthItems = projected
                .Where(p => p.Year == mStart.Year && p.Month == mStart.Month)
                .ToList();

            var byMember = monthItems
                .GroupBy(p => p.UserId)
                .Select(g =>
                {
                    var contributions = g.Select(p => p.Item).OrderBy(i => i.DueDate).ToList();
                    var totalDue = g.Sum(p => p.Amount);
                    var totalPaid = g.Where(p => p.IsPaid).Sum(p => p.Amount);
                    if (contributions.Count > 0) currency = contributions[0].Currency;
                    nameById.TryGetValue(g.Key, out var displayName);
                    return new GroupMemberContributionDto(g.Key, displayName, totalDue, totalPaid, contributions);
                })
                .OrderBy(m2 => m2.UserId)
                .ToList();

            var total = byMember.Sum(m2 => m2.TotalDue);
            result.Add(new GroupMonthlyContributionsDto(label, mStart, total, currency, byMember));
        }

        return result;
    }

    /// <summary>Allocation IDs whose share is fully settled (Σ reimbursements ≥ share) for the
    /// given occurrence. Does NOT include payer-covered shares — callers that know the
    /// payer apply that separately.</summary>
    private async Task<IReadOnlySet<Guid>> GetPaidAllocationIdsForChargeAsync(
        Guid expenseId, DateTime occurrenceDate, CancellationToken cancellationToken = default)
    {
        var occ = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc);
        var eid = ChargeId.Create(expenseId);

        var splits = await _db.Allocations
            .AsNoTracking()
            .Where(s => s.ChargeId == eid)
            .ToListAsync(cancellationToken);
        if (splits.Count == 0) return new HashSet<Guid>();

        var splitIds = splits.Select(s => s.Id.Value).ToList();
        var settledMap = await SettlementReads.GetSettledByAllocationOccurrenceAsync(_db, splitIds, cancellationToken);

        var settledBy = settledMap
            .Where(kv => kv.Key.Occurrence.Date == occ.Date)
            .ToDictionary(kv => kv.Key.AllocationId, kv => kv.Value.Settled);

        return splits
            .Where(s => settledBy.TryGetValue(s.Id.Value, out var st) && st >= s.Amount.Amount)
            .Select(s => s.Id.Value)
            .ToHashSet();
    }

    /// <summary>
    /// Per-member net balance within a group. Computes total owed/owed-to-caller from claimed splits.
    /// NetSettlement is positive when the member owes the caller, negative when the caller owes them.
    /// </summary>
    public async Task<MemberBalanceListDto> ListMemberBalancesAsync(
        GroupId groupId, Guid callerUserId, CancellationToken cancellationToken = default)
    {
        // Gather all active expenses in this group along with their splits.
        var expenses = await _db.Charges
            .AsNoTracking()
            .Where(e => e.GroupId == groupId && e.IsActive)
            .ToListAsync(cancellationToken);

        if (expenses.Count == 0)
            return new MemberBalanceListDto([], 0);

        var expenseIds = expenses.Select(e => e.Id).ToList();
        var splits = await _db.Allocations
            .AsNoTracking()
            .Where(s => expenseIds.Contains(s.ChargeId))
            .ToListAsync(cancellationToken);

        if (splits.Count == 0)
            return new MemberBalanceListDto([], 0);

        // Settled total per split (signed sum across occurrences — reversals net to
        // zero). Outstanding = share − settled; partial reimbursements reduce the debt.
        var splitIds = splits.Select(s => s.Id.Value).ToList();
        var settledMap = await SettlementReads.GetSettledByAllocationOccurrenceAsync(_db, splitIds, cancellationToken);
        var settledByAllocation = settledMap
            .GroupBy(kv => kv.Key.AllocationId)
            .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value.Settled));

        // A debt to the payer exists only once the vendor has actually been paid (the bill was
        // fronted/funded). Upcoming, unpaid bills create no balances. Derived from the ledger.
        var owedToVendor = await VendorPaymentReads.GetOwedToVendorByChargeAsync(_db, expenseIds.Select(e => e.Value).ToList(), cancellationToken);
        bool vendorPaidFor(Guid chargeId) =>
            !owedToVendor.TryGetValue(chargeId, out var owed) || owed <= 0.005m;

        // Naive balance model: for each split, the debtor owes the expense's payer the
        // OUTSTANDING amount. Group charges always have a payer (set at creation).
        var membersById = new Dictionary<Guid, MemberBalanceAccumulator>();

        foreach (var split in splits)
        {
            var expense = expenses.First(e => e.Id == split.ChargeId);
            var payerUserId = expense.PayerUserId;
            if (payerUserId is null) continue;

            // No debts until the bill is actually paid (the payer has fronted it / pot funded it).
            if (!vendorPaidFor(expense.Id.Value)) continue;

            var debtorUserId = split.UserId.Value;

            // Skip self-payer splits (the payer's own share is covered, not a debt).
            if (payerUserId == debtorUserId) continue;

            var settled = settledByAllocation.TryGetValue(split.Id.Value, out var st) ? st : 0m;
            var amount = split.Amount.Amount - settled;   // outstanding
            if (amount <= 0) continue;                     // fully settled
            var currency = split.Amount.Currency;

            if (payerUserId == callerUserId)
            {
                // Someone else owes the caller
                if (!membersById.TryGetValue(debtorUserId, out var cur))
                    cur = new MemberBalanceAccumulator(0m, 0m, currency);
                membersById[debtorUserId] = cur with { OwedByThem = cur.OwedByThem + amount, Currency = currency };
            }
            else if (debtorUserId == callerUserId)
            {
                // The caller owes someone else
                if (!membersById.TryGetValue(payerUserId.Value, out var cur))
                    cur = new MemberBalanceAccumulator(0m, 0m, currency);
                membersById[payerUserId.Value] = cur with { OwedToThem = cur.OwedToThem + amount, Currency = currency };
            }
        }

        if (membersById.Count == 0)
            return new MemberBalanceListDto([], 0);

        var userIds = membersById.Keys.Select(g => new UserId(g)).ToList();
        var projections = await _db.UserProjections
            .AsNoTracking()
            .Where(p => userIds.Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId.Value, p => p.GetFullName(), cancellationToken);

        var items = membersById
            .Select(kvp =>
            {
                projections.TryGetValue(kvp.Key, out var displayName);
                return new MemberBalanceDto(
                    UserId: kvp.Key,
                    DisplayName: displayName ?? "Member",
                    TotalOwed: kvp.Value.OwedByThem,
                    TotalOwedToYou: kvp.Value.OwedToThem,
                    NetSettlement: kvp.Value.OwedByThem - kvp.Value.OwedToThem,
                    Currency: kvp.Value.Currency);
            })
            .OrderBy(m => m.DisplayName)
            .ToList();

        return new MemberBalanceListDto(items, items.Count);
    }

    /// <summary>
    /// Returns the most recent fully-settled period (last day of the latest month where every split is claimed)
    /// or null when no period in the past 12 months is fully settled.
    /// </summary>
    public async Task<SettlementSummaryDto?> GetLastSettlementAsync(GroupId groupId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var windowStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-12);
        var windowEnd = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(-1);

        var contributions = await ListAllocationsByGroupAsync(groupId, windowStart, windowEnd, cancellationToken);

        // Walk months in reverse order and return the most recent one where all members have TotalDue == TotalPaid.
        var ordered = contributions.OrderByDescending(c => c.PeriodStart).ToList();
        foreach (var month in ordered)
        {
            if (month.Members.Count == 0) continue;
            if (month.Members.All(m => m.TotalDue == m.TotalPaid && m.TotalDue > 0))
            {
                var currency = month.Members.FirstOrDefault()?.Contributions.FirstOrDefault()?.Currency ?? "USD";
                return new SettlementSummaryDto(month.PeriodStart.AddMonths(1).AddDays(-1), month.Total, currency);
            }
        }
        return null;
    }

    private record struct MemberBalanceAccumulator(decimal OwedByThem, decimal OwedToThem, string Currency);

    private static IReadOnlyCollection<GroupMonthlyContributionsDto> BuildEmptyMonths(DateTime windowStart, DateTime windowEnd)
    {
        var monthCount = ((windowEnd.Year * 12 + windowEnd.Month) - (windowStart.Year * 12 + windowStart.Month)) + 1;
        return Enumerable.Range(0, monthCount)
            .Select(m =>
            {
                var mStart = windowStart.AddMonths(m);
                return new GroupMonthlyContributionsDto(mStart.ToString("MMMM yyyy"), mStart, 0, "USD", []);
            })
            .ToList();
    }
}
