using Finance.Application.Dtos;
using Finance.Application.Queries;
using Finance.Application.Mappers;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Finance.Infrastructure.Persistence.Projections;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class ExpenseQuery : IExpenseQuery
{
    private readonly FinanceDbContext _db;

    public ExpenseQuery(FinanceDbContext db) => _db = db;

    public async Task<ExpenseListDto> ListByUserAsync(ListExpensesParams request, CancellationToken cancellationToken = default)
    {
        var userId = UserId.Create(request.UserId);
        var query = _db.Expenses.Where(b => b.Owner.Kind == EntityKind.Person && b.Owner.Id == userId.Value);
        if (request.ActiveOnly) query = query.Where(b => b.IsActive);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(b => b.DueDate)
            .ThenBy(b => b.Title)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            return new ExpenseListDto([], total);

        var expenseIds = items.Select(b => b.Id).ToList();
        var occurrencesByExpenseId = items.ToDictionary(
            b => b.Id,
            b => b.OccurrenceDate);

        var paidExpenseIds = await PersonalExpenseReads.GetPaidAsync(
            _db, expenseIds.Select(id => id.Value).ToList(), cancellationToken);

        var responses = items
            .Select(b => ExpenseMapper.ToResponse(b, paidExpenseIds.Contains(b.Id.Value)))
            .ToArray();

        return new ExpenseListDto(responses, total);
    }

    public async Task<ExpenseResponseDto?> GetDetailAsync(ExpenseDetailParams request, CancellationToken cancellationToken = default)
    {
        var id = ExpenseId.Create(request.ExpenseId);
        var callerUserId = UserId.Create(request.CallerUserId);
        // `GroupId == null` says "personal", not "yours" — the owner predicate is what
        // makes it yours. Null (→ 404), never 403, so an id can't be confirmed.
        var expense = await _db.Expenses.AsNoTracking()
            .FirstOrDefaultAsync(
                b => b.Id == id && b.Owner.Kind == EntityKind.Person && b.Owner.Id == callerUserId.Value,
                cancellationToken);
        if (expense is null) return null;

        var paid = await PersonalExpenseReads.IsPaidAsync(_db, id.Value, cancellationToken);
        return ExpenseMapper.ToResponse(expense, paid);
    }

    public async Task<GroupExpenseListDto> ListByGroupAsync(ListGroupExpensesParams request, CancellationToken cancellationToken = default)
    {
        var groupId = GroupId.Create(request.GroupId);
        var query = _db.Expenses.Where(b => b.Owner.Kind == EntityKind.Group && b.Owner.Id == groupId.Value);
        if (request.ActiveOnly) query = query.Where(b => b.IsActive);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(b => b.DueDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            return new GroupExpenseListDto([], total);

        // Derived from the Vendor Payable balance — there is no stored paid-flag.
        var owedToVendor = await VendorPaymentReads.GetOwedToVendorByExpenseAsync(
            _db, items.Select(b => b.Id.Value).ToList(), cancellationToken);
        bool VendorPaidFor(Guid expenseId) =>
            !owedToVendor.TryGetValue(expenseId, out var owed) || owed <= 0.005m;

        HashSet<Guid> paidExpenseIds = [];
        // The caller's REAL share — the split may be uneven, so this is never derived
        // by dividing the total.
        Dictionary<Guid, decimal> callerShareByExpense = [];
        if (request.CallerUserId.HasValue)
        {
            var callerUserId = UserId.Create(request.CallerUserId.Value);
            var expenseIds = items.Select(b => b.Id).ToList();
            var callerShares = await _db.Shares
                .AsNoTracking()
                .Where(s => s.UserId == callerUserId && expenseIds.Contains(s.ExpenseId))
                .ToListAsync(cancellationToken);

            callerShareByExpense = callerShares
                .GroupBy(s => s.ExpenseId.Value)
                .ToDictionary(g => g.Key, g => g.Sum(s => s.Amount.Amount));

            if (callerShares.Count > 0)
            {
                var shareIds = callerShares.Select(s => s.Id.Value).ToList();
                var settledMap = await SettlementReads.GetSettledByShareOccurrenceAsync(
                    _db, shareIds, cancellationToken);

                foreach (var expense in items)
                {
                    var occurrence = expense.OccurrenceDate;
                    var share = callerShares.FirstOrDefault(s => s.ExpenseId == expense.Id);
                    if (share is null) continue;

                    // The payer's own share counts as covered only once the VENDOR is paid.
                    // Everyone else's is covered when settlements reach the share — summed
                    // signed, so a reversal nets its original to zero.
                    var callerIsPayer = expense.CoversOwnShare(callerUserId.Value);
                    var settled = settledMap.TryGetValue((share.Id.Value, occurrence.Date), out var sv) ? sv.Settled : 0m;
                    if ((callerIsPayer && VendorPaidFor(expense.Id.Value)) || settled >= share.Amount.Amount)
                        paidExpenseIds.Add(expense.Id.Value);
                }
            }
        }

        var responses = items.Select(b =>
        {
            var occurrence = b.OccurrenceDate;
            var isPaid = paidExpenseIds.Contains(b.Id.Value);
            var callerShare = callerShareByExpense.TryGetValue(b.Id.Value, out var cs) ? (decimal?)cs : null;
            return ExpenseMapper.ToResponse(b, isPaid, VendorPaidFor(b.Id.Value))
                with { CurrentOccurrenceDate = occurrence, CallerShare = callerShare };
        }).ToArray();

        return new GroupExpenseListDto(responses, total);
    }

    public async Task<ExpenseResponseDto?> GetGroupDetailAsync(GroupExpenseDetailParams request, CancellationToken cancellationToken = default)
    {
        var id = ExpenseId.Create(request.ExpenseId);
        var expense = await _db.Expenses.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && b.Owner.Kind == EntityKind.Group, cancellationToken);
        if (expense is null) return null;

        var owed = await VendorPaymentReads.GetOwedToVendorByExpenseAsync(_db, new[] { id.Value }, cancellationToken);
        var vendorPaid = !owed.TryGetValue(id.Value, out var v) || v <= 0.005m;
        return ExpenseMapper.ToResponse(expense, vendorPaid: vendorPaid);
    }

    public async Task<IReadOnlyCollection<ShareDto>> ListSharesAsync(ListSharesParams request, CancellationToken cancellationToken = default)
    {
        var expenseId = ExpenseId.Create(request.ExpenseId);
        var shares = await _db.Shares
            .AsNoTracking()
            .Where(s => s.ExpenseId == expenseId)
            .ToListAsync(cancellationToken);

        return shares.Select(ExpenseMapper.ToShareResponse).ToArray();
    }

    public async Task<GroupExpenseDetailDto?> GetGroupExpenseDetailAsync(Guid expenseId, Guid callerUserId, CancellationToken cancellationToken = default)
    {
        var expense = await GetGroupDetailAsync(new GroupExpenseDetailParams(expenseId), cancellationToken);
        if (expense is null) return null;

        var shares = await ListSharesAsync(new ListSharesParams(expenseId), cancellationToken);

        var occurrenceDate = expense.CurrentOccurrenceDate ?? expense.DueDate;
        var paidShareIds = await GetPaidShareIdsForExpenseAsync(expenseId, occurrenceDate, cancellationToken);
        var payerGuid = expense.PayerUserId;

        var names = await UserProjection.NamesAsync(
            _db.UserProjections, shares.Select(s => s.UserId).Distinct().ToList(), cancellationToken);

        var roles = expense.Scope == ExpenseScope.Group
            ? await GroupMemberProjection.RolesAsync(_db.GroupMemberProjections, expense.OwnerId, cancellationToken)
            : [];

        var enrichedShares = shares.Select(s =>
        {
            return new ShareDetailDto(
                s.ShareId,
                s.UserId,
                names.GetValueOrDefault(s.UserId),
                null,
                roles.GetValueOrDefault(s.UserId, "Member"),
                s.Amount,
                s.Currency,
                paidShareIds.Contains(s.ShareId) || (s.UserId == payerGuid && expense.VendorPaid));
        }).ToList();

        return new GroupExpenseDetailDto(expense, enrichedShares);
    }

    public async Task<ShareDetailDto?> GetShareDetailAsync(Guid shareId, CancellationToken cancellationToken = default)
    {
        var id = ShareId.Create(shareId);
        var share = await _db.Shares
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (share is null) return null;

        var expense = await _db.Expenses
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == share.ExpenseId, cancellationToken);
        if (expense is null) return null;

        var occurrenceDate = expense.OccurrenceDate;
        var paidShareIds = await GetPaidShareIdsForExpenseAsync(share.ExpenseId.Value, occurrenceDate, cancellationToken);

        var profiles = await UserProjection.ProfilesAsync(_db.UserProjections, [share.UserId.Value], cancellationToken);
        profiles.TryGetValue(share.UserId.Value, out var profile);

        var membershipRole = expense.Owner.IsGroup
            ? (await GroupMemberProjection.RolesAsync(_db.GroupMemberProjections, expense.Owner.Id, cancellationToken))
                .GetValueOrDefault(share.UserId.Value)
            : null;

        return new ShareDetailDto(
            share.Id.Value,
            share.UserId.Value,
            profile.Name,
            profile.AvatarUrl,
            membershipRole ?? "Member",
            share.Amount.Amount,
            share.Amount.Currency,
            paidShareIds.Contains(share.Id.Value));
    }

    public Task<bool> ExistsForUserAsync(UserId userId, string title, decimal amount, CancellationToken cancellationToken = default)
        => _db.Expenses.AsNoTracking()
            .AnyAsync(
                e => e.Owner.Kind == EntityKind.Person && e.Owner.Id == userId.Value
                    && e.IsActive && e.Title == title && e.Amount.Amount == amount,
                cancellationToken);

    public async Task<IReadOnlyCollection<GroupMonthlyContributionsDto>> ListSharesByGroupAsync(
        GroupId groupId, DateTime windowStart, DateTime windowEnd, CancellationToken cancellationToken = default)
    {
        // One expense is one occurrence, so the window is a plain date filter the database can run.
        var relevantExpenses = await _db.Expenses
            .AsNoTracking()
            .Where(b => b.Owner.Kind == EntityKind.Group && b.Owner.Id == groupId.Value && b.IsActive
                        && b.OccurrenceDate >= windowStart && b.OccurrenceDate <= windowEnd)
            .ToListAsync(cancellationToken);

        if (relevantExpenses.Count == 0) return BuildEmptyMonths(windowStart, windowEnd);

        var expenseIds = relevantExpenses.Select(b => b.Id).ToList();

        var shares = await _db.Shares
            .AsNoTracking()
            .Where(s => expenseIds.Contains(s.ExpenseId))
            .ToListAsync(cancellationToken);

        if (shares.Count == 0) return BuildEmptyMonths(windowStart, windowEnd);

        var expenseById = relevantExpenses.ToDictionary(b => b.Id);
        var nameById = await UserProjection.NamesAsync(
            _db.UserProjections, shares.Select(s => s.UserId.Value).Distinct().ToList(), cancellationToken);

        var shareIds = shares.Select(s => s.Id.Value).ToList();

        // Settled total per (share, occurrence) from the ledger — signed so reversals net to
        // zero. A share is "paid" when settled ≥ share (partial-aware).
        var settledByShareOccurrence = await SettlementReads.GetSettledByShareOccurrenceAsync(
            _db, shareIds, cancellationToken);

        // The payer's own share counts as covered only once the expense itself is paid.
        var owedToVendor = await VendorPaymentReads.GetOwedToVendorByExpenseAsync(
            _db, expenseIds.Select(e => e.Value).ToList(), cancellationToken);
        bool vendorPaidFor(Guid expenseId) =>
            !owedToVendor.TryGetValue(expenseId, out var owed) || owed <= 0.005m;

        var windowEndExclusive = windowEnd.AddDays(1);

        var projected = new List<(int Year, int Month, Guid UserId, bool IsPaid, decimal Amount, string Currency, ContributionItemDto Item)>();

        foreach (var share in shares)
        {
            if (!expenseById.TryGetValue(share.ExpenseId, out var expense)) continue;

            IEnumerable<DateTime> occurrenceDates = [expense.OccurrenceDate];

            // Before the vendor is paid the expense is upcoming and no share is settled.
            var isPayerOwnShare = expense.CoversOwnShare(share.UserId.Value) && vendorPaidFor(expense.Id.Value);

            foreach (var date in occurrenceDates)
            {
                var settled = settledByShareOccurrence.TryGetValue((share.Id.Value, date.Date), out var sv) ? sv.Settled : 0m;
                var isPaid = isPayerOwnShare || settled >= share.Amount.Amount;
                projected.Add((date.Year, date.Month, share.UserId.Value, isPaid,
                    share.Amount.Amount, share.Amount.Currency,
                    new ContributionItemDto(
                        share.Id.Value, expense.Id.Value,
                        expense.Title, expense.Category.ToString(),
                        share.Amount.Amount, share.Amount.Currency,
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

    /// <summary>Share IDs whose share is fully settled (Σ settlements ≥ share) for the
    /// given occurrence. Does NOT include payer-covered shares — callers that know the
    /// payer apply that separately.</summary>
    private async Task<IReadOnlySet<Guid>> GetPaidShareIdsForExpenseAsync(
        Guid expenseId, DateTime occurrenceDate, CancellationToken cancellationToken = default)
    {
        var occ = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc);
        var eid = ExpenseId.Create(expenseId);

        var shares = await _db.Shares
            .AsNoTracking()
            .Where(s => s.ExpenseId == eid)
            .ToListAsync(cancellationToken);
        if (shares.Count == 0) return new HashSet<Guid>();

        var shareIds = shares.Select(s => s.Id.Value).ToList();
        var settledMap = await SettlementReads.GetSettledByShareOccurrenceAsync(_db, shareIds, cancellationToken);

        var settledBy = settledMap
            .Where(kv => kv.Key.Occurrence.Date == occ.Date)
            .ToDictionary(kv => kv.Key.ShareId, kv => kv.Value.Settled);

        return shares
            .Where(s => settledBy.TryGetValue(s.Id.Value, out var st) && st >= s.Amount.Amount)
            .Select(s => s.Id.Value)
            .ToHashSet();
    }

    /// <summary>
    /// Per-member net balance within a group. Computes total owed/owed-to-caller from shares net of settlement.
    /// NetSettlement is positive when the member owes the caller, negative when the caller owes them.
    /// </summary>
    public async Task<MemberBalanceListDto> ListMemberBalancesAsync(
        GroupId groupId, Guid callerUserId, CancellationToken cancellationToken = default)
    {
        // Gather all active expenses in this group along with their shares.
        var expenses = await _db.Expenses
            .AsNoTracking()
            // Owner.Kind/Owner.Id rather than the computed GroupId, which has no column and
            // cannot be translated — see GroupQuery.ExpenseBelongsToGroupAsync.
            .Where(e => e.Owner.Kind == EntityKind.Group && e.Owner.Id == groupId.Value && e.IsActive)
            .ToListAsync(cancellationToken);

        if (expenses.Count == 0)
            return new MemberBalanceListDto([], 0);

        var expenseIds = expenses.Select(e => e.Id).ToList();
        var shares = await _db.Shares
            .AsNoTracking()
            .Where(s => expenseIds.Contains(s.ExpenseId))
            .ToListAsync(cancellationToken);

        if (shares.Count == 0)
            return new MemberBalanceListDto([], 0);

        // Settled total per share (signed sum across occurrences — reversals net to
        // zero). Outstanding = share − settled; partial settlements reduce the debt.
        var shareIds = shares.Select(s => s.Id.Value).ToList();
        var settledMap = await SettlementReads.GetSettledByShareOccurrenceAsync(_db, shareIds, cancellationToken);
        var settledByShare = settledMap
            .GroupBy(kv => kv.Key.ShareId)
            .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value.Settled));

        // A debt to the payer exists only once the vendor has actually been paid (the expense was
        // fronted/funded). Upcoming, unpaid expenses create no balances. Derived from the ledger.
        var owedToVendor = await VendorPaymentReads.GetOwedToVendorByExpenseAsync(_db, expenseIds.Select(e => e.Value).ToList(), cancellationToken);
        bool vendorPaidFor(Guid expenseId) =>
            !owedToVendor.TryGetValue(expenseId, out var owed) || owed <= 0.005m;

        // Naive balance model: for each share, the debtor owes the expense's payer the
        // OUTSTANDING amount. Group expenses always have a payer (set at creation).
        var membersById = new Dictionary<Guid, MemberBalanceAccumulator>();

        foreach (var share in shares)
        {
            var expense = expenses.First(e => e.Id == share.ExpenseId);
            var payerUserId = expense.PayerUserId;
            if (payerUserId is null) continue;

            // No debts until the expense is actually paid (the payer has fronted it / pot funded it).
            if (!vendorPaidFor(expense.Id.Value)) continue;

            var debtorUserId = share.UserId.Value;

            // Skip self-payer shares (the payer's own share is covered, not a debt).
            if (payerUserId == debtorUserId) continue;

            var settled = settledByShare.TryGetValue(share.Id.Value, out var st) ? st : 0m;
            var amount = share.Amount.Amount - settled;   // outstanding
            if (amount <= 0) continue;                     // fully settled
            var currency = share.Amount.Currency;

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

        var projections = await UserProjection.NamesAsync(_db.UserProjections, membersById.Keys.ToList(), cancellationToken);

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
    /// Returns the most recent fully-settled period (last day of the latest month where every share is settled)
    /// or null when no period in the past 12 months is fully settled.
    /// </summary>
    public async Task<SettlementSummaryDto?> GetLastSettlementAsync(GroupId groupId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var windowStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-12);
        var windowEnd = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(-1);

        var contributions = await ListSharesByGroupAsync(groupId, windowStart, windowEnd, cancellationToken);

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
