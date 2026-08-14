using Finance.Application.Dtos;
using Finance.Application.Managers;
using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Tests;

/// <summary>
/// Generation is driven by somebody acting, not by a clock, and it must be safe to call twice —
/// two people paying the same month at once must not produce two bills.
/// </summary>
public class RecurringExpenseManagerTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Group = Guid.NewGuid();
    private static readonly DateTime Jan3 = new(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc);

    private static RecurringExpenseManager NewManager(out FakeScheduleRepo schedules, out FakeExpenseRepo expenses)
    {
        schedules = new FakeScheduleRepo();
        expenses = new FakeExpenseRepo(schedules);
        // Bookkeeping is only reached by CatchUpPersonalAsync, which these do not exercise.
        return new RecurringExpenseManager(schedules, expenses, null!);
    }

    private static CreateRecurringExpenseCommand Rent(decimal amount = 1000m) => new(
        GroupId: Group, CallerUserId: User, Title: "Rent", Amount: amount, Currency: "USD",
        Category: ExpenseCategory.Rent, Frequency: RecurrenceFrequency.Monthly, AnchorDate: Jan3);

    [Fact]
    public async Task Materialise_WritesTheExpenseWithTheScheduleAmount()
    {
        var manager = NewManager(out _, out var expenses);
        var schedule = await manager.CreateAsync(Rent());

        var expense = await manager.MaterialiseAsync(schedule.RecurringExpenseId, Jan3);

        Assert.NotNull(expense);
        Assert.Equal(1000m, expense!.Amount.Amount);
        Assert.Equal(Jan3, expense.OccurrenceDate);
        Assert.Single(expenses.Saved);
    }

    [Fact]
    public async Task Materialise_IsIdempotent()
    {
        var manager = NewManager(out _, out var expenses);
        var schedule = await manager.CreateAsync(Rent());

        var first = await manager.MaterialiseAsync(schedule.RecurringExpenseId, Jan3);
        var second = await manager.MaterialiseAsync(schedule.RecurringExpenseId, Jan3);

        Assert.Equal(first!.Id, second!.Id);
        Assert.Single(expenses.Saved);
    }

    [Fact]
    public async Task Materialise_RefusesADateTheScheduleDoesNotPlaceAExpense()
    {
        var manager = NewManager(out _, out _);
        var schedule = await manager.CreateAsync(Rent());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.MaterialiseAsync(schedule.RecurringExpenseId, Jan3.AddDays(5)));
    }

    [Fact]
    public async Task AmendingAfterMaterialising_LeavesTheRecordedMonthAlone()
    {
        var manager = NewManager(out _, out _);
        var schedule = await manager.CreateAsync(Rent());
        var january = await manager.MaterialiseAsync(schedule.RecurringExpenseId, Jan3);

        await manager.AmendAsync(new AmendRecurringExpenseCommand(
            schedule.RecurringExpenseId, User, "Rent", 1100m, "USD", ExpenseCategory.Rent,
            EffectiveFrom: Jan3.AddMonths(1)));

        var feb = await manager.MaterialiseAsync(schedule.RecurringExpenseId, Jan3.AddMonths(1));

        Assert.Equal(1000m, january!.Amount.Amount);
        Assert.Equal(1100m, feb!.Amount.Amount);
    }

    [Fact]
    public async Task Forecast_ReportsWhatWasBilled_ForRecordedMonthsAndTheScheduleForTherest()
    {
        var manager = NewManager(out _, out _);
        var schedule = await manager.CreateAsync(Rent());
        await manager.MaterialiseAsync(schedule.RecurringExpenseId, Jan3);
        await manager.AmendAsync(new AmendRecurringExpenseCommand(
            schedule.RecurringExpenseId, User, "Rent", 1100m, "USD", ExpenseCategory.Rent,
            EffectiveFrom: Jan3.AddMonths(1)));

        var forecast = await manager.ForecastAsync(schedule.RecurringExpenseId, Jan3, Jan3.AddMonths(3));

        Assert.Equal(3, forecast.Count);
        // January was billed at 1,000 and says so; the months not yet recorded quote the schedule.
        Assert.Equal(1000m, forecast[0].Amount);
        Assert.NotNull(forecast[0].ExpenseId);
        Assert.Equal(1100m, forecast[1].Amount);
        Assert.Null(forecast[1].ExpenseId);
    }

    [Fact]
    public async Task Deactivate_StopsFutureOccurrencesAndKeepsWhatWasRecorded()
    {
        var manager = NewManager(out _, out var expenses);
        var schedule = await manager.CreateAsync(Rent());
        await manager.MaterialiseAsync(schedule.RecurringExpenseId, Jan3);

        await manager.DeactivateAsync(schedule.RecurringExpenseId, User);

        Assert.Empty(await manager.ForecastAsync(schedule.RecurringExpenseId, Jan3, Jan3.AddMonths(6)));
        Assert.Single(expenses.Saved);
    }

    [Fact]
    public async Task CatchUp_WritesEveryPeriodThatHasPassed()
    {
        var manager = NewManager(out _, out var expenses);
        var schedule = await manager.CreateAsync(Rent());

        // Three months on: January, February and March have all come due.
        var written = await manager.CatchUpAsync(Group, User, Jan3.AddMonths(2));

        Assert.Equal(3, written);
        Assert.Equal(
            [Jan3, Jan3.AddMonths(1), Jan3.AddMonths(2)],
            expenses.Saved.Select(c => c.OccurrenceDate).OrderBy(d => d));
    }

    [Fact]
    public async Task CatchUp_StopsAtToday_SoNothingUnhappenedIsOnTheBooks()
    {
        var manager = NewManager(out _, out var expenses);
        var schedule = await manager.CreateAsync(Rent());

        await manager.CatchUpAsync(Group, User, Jan3.AddDays(20));

        // February's rent has not happened. Writing it would put a cost in the books that nobody
        // has incurred.
        Assert.Single(expenses.Saved);
        Assert.Equal(Jan3, expenses.Saved[0].OccurrenceDate);
    }

    [Fact]
    public async Task CatchUp_IsIdempotent_SoTwoLoadsDoNotDoubleBill()
    {
        var manager = NewManager(out _, out var expenses);
        await manager.CreateAsync(Rent());

        await manager.CatchUpAsync(Group, User, Jan3.AddMonths(2));
        var second = await manager.CatchUpAsync(Group, User, Jan3.AddMonths(2));

        Assert.Equal(0, second);
        Assert.Equal(3, expenses.Saved.Count);
    }

    [Fact]
    public async Task CatchUp_BillsEachPeriodAtWhatWasAgreedThen()
    {
        var manager = NewManager(out _, out var expenses);
        var schedule = await manager.CreateAsync(Rent());
        await manager.AmendAsync(new AmendRecurringExpenseCommand(
            schedule.RecurringExpenseId, User, "Rent", 1100m, "USD", ExpenseCategory.Rent,
            EffectiveFrom: Jan3.AddMonths(2)));

        await manager.CatchUpAsync(Group, User, Jan3.AddMonths(2));

        var byDate = expenses.Saved.ToDictionary(c => c.OccurrenceDate, c => c.Amount.Amount);
        Assert.Equal(1000m, byDate[Jan3]);
        Assert.Equal(1000m, byDate[Jan3.AddMonths(1)]);
        Assert.Equal(1100m, byDate[Jan3.AddMonths(2)]);
    }

    [Fact]
    public async Task CatchUp_SkipsADeactivatedSchedule()
    {
        var manager = NewManager(out _, out var expenses);
        var schedule = await manager.CreateAsync(Rent());
        await manager.DeactivateAsync(schedule.RecurringExpenseId, User);

        Assert.Equal(0, await manager.CatchUpAsync(Group, User, Jan3.AddMonths(3)));
        Assert.Empty(expenses.Saved);
    }

    internal sealed class FakeScheduleRepo : IRecurringExpenseRepository
    {
        public List<RecurringExpense> Schedules { get; } = new();
        public List<Expense> Expenses { get; } = new();

        public Task AddAsync(RecurringExpense s, CancellationToken ct = default) { Schedules.Add(s); return Task.CompletedTask; }
        public Task<RecurringExpense?> GetByIdAsync(RecurringExpenseId id, CancellationToken ct = default)
            => Task.FromResult(Schedules.FirstOrDefault(s => s.Id == id));
        public Task<IReadOnlyList<RecurringExpense>> ListForGroupAsync(GroupId g, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RecurringExpense>>(Schedules.Where(s => s.GroupId == g).ToList());
        public Task<IReadOnlyList<RecurringExpense>> ListForUserAsync(UserId u, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RecurringExpense>>(Schedules.Where(s => s.CreatedBy == u && s.GroupId == null).ToList());
        public Task<Expense?> GetGeneratedAsync(RecurringExpenseId id, DateTime date, CancellationToken ct = default)
            => Task.FromResult(Expenses.FirstOrDefault(c => c.RecurringExpenseId == id && c.OccurrenceDate == date.Date));
        public Task<IReadOnlyDictionary<DateTime, Expense>> ListGeneratedAsync(
            RecurringExpenseId id, DateTime from, DateTime to, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<DateTime, Expense>>(
                Expenses.Where(c => c.RecurringExpenseId == id && c.OccurrenceDate >= from.Date && c.OccurrenceDate <= to.Date)
                       .ToDictionary(c => c.OccurrenceDate));
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    internal sealed class FakeExpenseRepo(FakeScheduleRepo shared) : IExpenseRepository
    {
        public List<Expense> Saved => shared.Expenses;

        public Task AddAsync(Expense c, CancellationToken ct = default) { shared.Expenses.Add(c); return Task.CompletedTask; }
        public Task UpdateAsync(Expense c, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(Expense c, CancellationToken ct = default) { shared.Expenses.Remove(c); return Task.CompletedTask; }
        public Task<Expense?> GetByIdAsync(ExpenseId id, CancellationToken ct = default)
            => Task.FromResult(shared.Expenses.FirstOrDefault(c => c.Id == id));
        public Task<IReadOnlyList<Expense>> ListUnpostedPersonalAsync(UserId userId, DateTime asOf, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Expense>>(
                shared.Expenses.Where(c => c.Owner == AccountingEntity.Person(userId) && c.IsActive && c.OccurrenceDate.Date <= asOf.Date).ToList());
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAllForUserAsync(UserId u, CancellationToken ct = default) => Task.CompletedTask;
    }
}
