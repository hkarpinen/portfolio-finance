using Finance.Application.Dtos;
using Finance.Application.Queries;
using Finance.Application.Mappers;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
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
        var query = _db.Expenses.Where(b => b.Owner.Kind == EntityKind.Household && b.Owner.Id == groupId.Value);
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
                var splitIds = callerShares.Select(s => s.Id.Value).ToList();
                var settledMap = await SettlementReads.GetSettledByShareOccurrenceAsync(
                    _db, splitIds, cancellationToken);

                foreach (var expense in items)
                {
                    var occurrence = expense.OccurrenceDate;
                    var split = callerShares.FirstOrDefault(s => s.ExpenseId == expense.Id);
                    if (split is null) continue;

                    // The payer's own share counts as covered only once the VENDOR is paid.
                    // Everyone else's is covered when settlements reach the share — summed
                    // signed, so a reversal nets its original to zero.
                    var callerIsPayer = expense.PayerUserId == callerUserId.Value;
                    var settled = settledMap.TryGetValue((split.Id.Value, occurrence.Date), out var sv) ? sv.Settled : 0m;
                    if ((callerIsPayer && VendorPaidFor(expense.Id.Value)) || settled >= split.Amount.Amount)
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
        var expense = await _db.Expenses.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id && b.Owner.Kind == EntityKind.Household, cancellationToken);
        if (expense is null) return null;

        var owed = await VendorPaymentReads.GetOwedToVendorByExpenseAsync(_db, new[] { id.Value }, cancellationToken);
        var vendorPaid = !owed.TryGetValue(id.Value, out var v) || v <= 0.005m;
        return ExpenseMapper.ToResponse(expense, vendorPaid: vendorPaid);
    }

    public async Task<IReadOnlyCollection<ShareDto>> ListSharesAsync(ListSharesParams request, CancellationToken cancellationToken = default)
    {
        var expenseId = ExpenseId.Create(request.ExpenseId);
        var splits = await _db.Shares
            .AsNoTracking()
            .Where(s => s.ExpenseId == expenseId)
            .ToListAsync(cancellationToken);

        return splits.Select(ExpenseMapper.ToShareResponse).ToArray();
    }

    public async Task<GroupExpenseDetailDto?> GetGroupExpenseDetailAsync(Guid expenseId, Guid callerUserId, CancellationToken cancellationToken = default)
    {
        var expense = await GetGroupDetailAsync(new GroupExpenseDetailParams(expenseId), cancellationToken);
        if (expense is null) return null;

        var splits = await ListSharesAsync(new ListSharesParams(expenseId), cancellationToken);

        var occurrenceDate = expense.CurrentOccurrenceDate ?? expense.DueDate;
        var paidShareIds = await GetPaidShareIdsForExpenseAsync(expenseId, occurrenceDate, cancellationToken);
        var payerGuid = expense.PayerUserId;

        var splitUserIdSet = splits.Select(s => s.UserId).ToHashSet();
        var projections2 = (await _db.UserProjections.AsNoTracking()
            .ToListAsync(cancellationToken))
            .Where(p => splitUserIdSet.Contains(p.UserId.Value))
            .ToDictionary(p => p.UserId.Value);

        // "Member" is the fallback for rows predating the membership projection.
        var roles = expense.Scope == ExpenseScope.Group
            ? await _db.GroupMemberProjections.AsNoTracking()
                .Where(m => m.GroupId == expense.OwnerId)
                .ToDictionaryAsync(m => m.UserId, m => m.Role, cancellationToken)
            : new Dictionary<Guid, string>();

        var enrichedShares = splits.Select(s =>
        {
            projections2.TryGetValue(s.UserId, out var proj);
            return new ShareDetailDto(
                s.ShareId,
                s.UserId,
                proj?.GetFullName(),
                null,
                roles.GetValueOrDefault(s.UserId, "Member"),
                s.Amount,
                s.Currency,
                paidShareIds.Contains(s.ShareId) || (s.UserId == payerGuid && expense.VendorPaid));
        }).ToList();

        return new GroupExpenseDetailDto(expense, enrichedShares);
    }

    public async Task<ShareDetailDto?> GetShareDetailAsync(Guid splitId, CancellationToken cancellationToken = default)
    {
        var id = ShareId.Create(splitId);
        var split = await _db.Shares
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (split is null) return null;

        var expense = await _db.Expenses
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == split.ExpenseId, cancellationToken);
        if (expense is null) return null;

        var occurrenceDate = expense.OccurrenceDate;
        var paidShareIds = await GetPaidShareIdsForExpenseAsync(split.ExpenseId.Value, occurrenceDate, cancellationToken);

        var projection = await _db.UserProjections
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == split.UserId, cancellationToken);

        var membershipRole = expense.Owner.IsHousehold
            ? await _db.GroupMemberProjections.AsNoTracking()
                .Where(m => m.GroupId == expense.Owner.Id && m.UserId == split.UserId.Value)
                .Select(m => m.Role)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        return new ShareDetailDto(
            split.Id.Value,
            split.UserId.Value,
            projection?.GetFullName(),
            projection?.AvatarUrl,
            membershipRole ?? "Member",
            split.Amount.Amount,
            split.Amount.Currency,
            paidShareIds.Contains(split.Id.Value));
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
            .Where(b => b.Owner.Kind == EntityKind.Household && b.Owner.Id == groupId.Value && b.IsActive
                        && b.OccurrenceDate >= windowStart && b.OccurrenceDate <= windowEnd)
            .ToListAsync(cancellationToken);

        if (relevantExpenses.Count == 0) return BuildEmptyMonths(windowStart, windowEnd);

        var expenseIds = relevantExpenses.Select(b => b.Id).ToList();

        var splits = await _db.Shares
            .AsNoTracking()
            .Where(s => expenseIds.Contains(s.ExpenseId))
            .ToListAsync(cancellationToken);

        if (splits.Count == 0) return BuildEmptyMonths(windowStart, windowEnd);

        var expenseById = relevantExpenses.ToDictionary(b => b.Id);
        var splitUserIds = splits.Select(s => s.UserId).Distinct().ToList();

        var userProjections = await _db.UserProjections
            .AsNoTracking()
            .Where(p => splitUserIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);

        var nameById = userProjections.ToDictionary(p => p.UserId.Value, p => p.GetFullName());

        var splitIds = splits.Select(s => s.Id.Value).ToList();

        // Settled total per (share, occurrence) from the ledger — signed so reversals net to
        // zero. A share is "paid" when settled ≥ share (partial-aware).
        var settledByShareOccurrence = await SettlementReads.GetSettledByShareOccurrenceAsync(
            _db, splitIds, cancellationToken);

        // The payer's own share counts as covered only once the bill itself is paid.
        var owedToVendor = await VendorPaymentReads.GetOwedToVendorByExpenseAsync(
            _db, expenseIds.Select(e => e.Value).ToList(), cancellationToken);
        bool vendorPaidFor(Guid expenseId) =>
            !owedToVendor.TryGetValue(expenseId, out var owed) || owed <= 0.005m;

        var windowEndExclusive = windowEnd.AddDays(1);

        var projected = new List<(int Year, int Month, Guid UserId, bool IsPaid, decimal Amount, string Currency, ContributionItemDto Item)>();

        foreach (var split in splits)
        {
            if (!expenseById.TryGetValue(split.ExpenseId, out var expense)) continue;

            IEnumerable<DateTime> occurrenceDates = [expense.OccurrenceDate];

            // Before the vendor is paid the bill is upcoming and no share is settled.
            var isPayerOwnShare = expense.PayerUserId == split.UserId.Value && vendorPaidFor(expense.Id.Value);

            foreach (var date in occurrenceDates)
            {
                var settled = settledByShareOccurrence.TryGetValue((split.Id.Value, date.Date), out var sv) ? sv.Settled : 0m;
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

    /// <summary>Share IDs whose share is fully settled (Σ reimbursements ≥ share) for the
    /// given occurrence. Does NOT include payer-covered shares — callers that know the
    /// payer apply that separately.</summary>
    private async Task<IReadOnlySet<Guid>> GetPaidShareIdsForExpenseAsync(
        Guid expenseId, DateTime occurrenceDate, CancellationToken cancellationToken = default)
    {
        var occ = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc);
        var eid = ExpenseId.Create(expenseId);

        var splits = await _db.Shares
            .AsNoTracking()
            .Where(s => s.ExpenseId == eid)
            .ToListAsync(cancellationToken);
        if (splits.Count == 0) return new HashSet<Guid>();

        var splitIds = splits.Select(s => s.Id.Value).ToList();
        var settledMap = await SettlementReads.GetSettledByShareOccurrenceAsync(_db, splitIds, cancellationToken);

        var settledBy = settledMap
            .Where(kv => kv.Key.Occurrence.Date == occ.Date)
            .ToDictionary(kv => kv.Key.ShareId, kv => kv.Value.Settled);

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
        var expenses = await _db.Expenses
            .AsNoTracking()
            .Where(e => e.GroupId == groupId && e.IsActive)
            .ToListAsync(cancellationToken);

        if (expenses.Count == 0)
            return new MemberBalanceListDto([], 0);

        var expenseIds = expenses.Select(e => e.Id).ToList();
        var splits = await _db.Shares
            .AsNoTracking()
            .Where(s => expenseIds.Contains(s.ExpenseId))
            .ToListAsync(cancellationToken);

        if (splits.Count == 0)
            return new MemberBalanceListDto([], 0);

        // Settled total per split (signed sum across occurrences — reversals net to
        // zero). Outstanding = share − settled; partial reimbursements reduce the debt.
        var splitIds = splits.Select(s => s.Id.Value).ToList();
        var settledMap = await SettlementReads.GetSettledByShareOccurrenceAsync(_db, splitIds, cancellationToken);
        var settledByShare = settledMap
            .GroupBy(kv => kv.Key.ShareId)
            .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value.Settled));

        // A debt to the payer exists only once the vendor has actually been paid (the bill was
        // fronted/funded). Upcoming, unpaid bills create no balances. Derived from the ledger.
        var owedToVendor = await VendorPaymentReads.GetOwedToVendorByExpenseAsync(_db, expenseIds.Select(e => e.Value).ToList(), cancellationToken);
        bool vendorPaidFor(Guid expenseId) =>
            !owedToVendor.TryGetValue(expenseId, out var owed) || owed <= 0.005m;

        // Naive balance model: for each split, the debtor owes the expense's payer the
        // OUTSTANDING amount. Group expenses always have a payer (set at creation).
        var membersById = new Dictionary<Guid, MemberBalanceAccumulator>();

        foreach (var split in splits)
        {
            var expense = expenses.First(e => e.Id == split.ExpenseId);
            var payerUserId = expense.PayerUserId;
            if (payerUserId is null) continue;

            // No debts until the bill is actually paid (the payer has fronted it / pot funded it).
            if (!vendorPaidFor(expense.Id.Value)) continue;

            var debtorUserId = split.UserId.Value;

            // Skip self-payer splits (the payer's own share is covered, not a debt).
            if (payerUserId == debtorUserId) continue;

            var settled = settledByShare.TryGetValue(split.Id.Value, out var st) ? st : 0m;
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
