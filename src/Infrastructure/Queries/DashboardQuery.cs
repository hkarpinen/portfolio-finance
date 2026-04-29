using Finance.Application.Contracts;
using Finance.Application.Managers.Dependencies;
using Finance.Application.Queries;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class DashboardQuery : IDashboardQuery
{
    private readonly FinanceDbContext _db;
    private readonly IHouseholdCoverageEngine _coverageEngine;

    public DashboardQuery(FinanceDbContext db, IHouseholdCoverageEngine coverageEngine)
    {
        _db = db;
        _coverageEngine = coverageEngine;
    }

    public async Task<DashboardResponse> QueryAsync(DashboardQueryRequest request, CancellationToken cancellationToken = default)
    {
        var totalIncome = await GetTotalIncomeAsync(request.HouseholdId, request.PeriodStart, request.PeriodEnd, cancellationToken);
        var totalBills = await GetTotalBillsAsync(request.HouseholdId, request.PeriodStart, request.PeriodEnd, cancellationToken);

        var isOvercommitted = totalBills.Amount > totalIncome.Amount;
        var coverage = _coverageEngine.BuildCoverageStatus(
            request.HouseholdId, totalIncome, totalBills, request.PeriodStart, request.PeriodEnd);

        return new DashboardResponse(
            request.HouseholdId,
            totalIncome.Amount,
            totalBills.Amount,
            totalIncome.Amount - totalBills.Amount,
            isOvercommitted,
            coverage,
            request.PeriodStart,
            request.PeriodEnd);
    }

    public async Task<CoverageStatusResponse> GetCoverageStatusAsync(CoverageStatusQueryRequest request, CancellationToken cancellationToken = default)
    {
        var totalIncome = await GetTotalIncomeAsync(request.HouseholdId, request.PeriodStart, request.PeriodEnd, cancellationToken);
        var totalBills = await GetTotalBillsAsync(request.HouseholdId, request.PeriodStart, request.PeriodEnd, cancellationToken);

        return _coverageEngine.BuildCoverageStatus(
            request.HouseholdId, totalIncome, totalBills, request.PeriodStart, request.PeriodEnd);
    }

    private async Task<Money> GetTotalIncomeAsync(Guid householdId, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken)
    {
        // Income is personal — it is never owned by a household (HouseholdId is always null).
        // The household's "total income" is the sum of all active income sources belonging
        // to each member of that household.
        var memberUserIds = await _db.HouseholdMemberships
            .Where(m => m.HouseholdId == HouseholdId.Create(householdId) && m.IsActive)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        var items = await _db.IncomeSources
            .Where(i => memberUserIds.Contains(i.UserId) && i.IsActive)
            .ToListAsync(cancellationToken);

        decimal total = 0;
        string currency = "USD";

        var periodMonths = Math.Max(1,
            (periodEnd.Year * 12 + periodEnd.Month) - (periodStart.Year * 12 + periodStart.Month) + 1);

        foreach (var income in items)
        {
            currency = income.Amount.Currency;
            if (income.RecurrenceSchedule.StartDate > periodEnd) continue;
            if (income.RecurrenceSchedule.EndDate.HasValue && income.RecurrenceSchedule.EndDate.Value < periodStart) continue;

            var monthly = UserBudgetCalculator.MonthlyEquivalent(income.Amount.Amount, income.RecurrenceSchedule.Frequency);
            total += monthly * periodMonths;
        }

        return Money.Create(total, currency);
    }

    private async Task<Money> GetTotalBillsAsync(Guid householdId, DateTime periodStart, DateTime periodEnd, CancellationToken cancellationToken)
    {
        var items = await _db.Bills
            .Where(b => b.HouseholdId == HouseholdId.Create(householdId) && b.IsActive)
            .ToListAsync(cancellationToken);

        decimal total = 0;
        string currency = "USD";

        foreach (var bill in items)
        {
            currency = bill.Amount.Currency;
            var recurrence = bill.RecurrenceSchedule;

            if (recurrence == null)
            {
                if (bill.DueDate >= periodStart && bill.DueDate <= periodEnd)
                    total += bill.Amount.Amount;
                continue;
            }

            total += recurrence.GetAmountForPeriod(bill.Amount, periodStart, periodEnd);
        }

        return Money.Create(total, currency);
    }
}
