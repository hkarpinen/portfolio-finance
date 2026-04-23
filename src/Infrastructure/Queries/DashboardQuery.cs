using Bills.Application.Contracts;
using Bills.Application.Managers.Dependencies;
using Bills.Application.Queries;
using Bills.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Queries;

internal sealed class DashboardQuery : IDashboardQuery
{
    private readonly BillsDbContext _db;
    private readonly IHouseholdCoverageEngine _coverageEngine;

    public DashboardQuery(BillsDbContext db, IHouseholdCoverageEngine coverageEngine)
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
        var items = await _db.IncomeSources
            .Where(i => i.HouseholdId == HouseholdId.Create(householdId) && i.IsActive)
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

            var monthly = MonthlyEquivalent(income.Amount.Amount, income.RecurrenceSchedule.Frequency);
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

    private static decimal MonthlyEquivalent(decimal amount, RecurrenceFrequency frequency) => frequency switch
    {
        RecurrenceFrequency.Weekly       => amount * 52m / 12m,
        RecurrenceFrequency.BiWeekly     => amount * 26m / 12m,
        RecurrenceFrequency.Annually     => amount / 12m,
        RecurrenceFrequency.Quarterly    => amount / 3m,
        RecurrenceFrequency.SemiAnnually => amount / 6m,
        _                                => amount,
    };
}
