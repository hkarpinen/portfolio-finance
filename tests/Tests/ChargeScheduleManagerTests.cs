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
public class ChargeScheduleManagerTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Group = Guid.NewGuid();
    private static readonly DateTime Jan3 = new(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc);

    private static ChargeScheduleManager NewManager(out FakeScheduleRepo schedules, out FakeChargeRepo charges)
    {
        schedules = new FakeScheduleRepo();
        charges = new FakeChargeRepo(schedules);
        return new ChargeScheduleManager(schedules, charges);
    }

    private static CreateChargeScheduleCommand Rent(decimal amount = 1000m) => new(
        GroupId: Group, CallerUserId: User, Title: "Rent", Amount: amount, Currency: "USD",
        Category: ChargeCategory.Rent, Frequency: RecurrenceFrequency.Monthly, AnchorDate: Jan3);

    [Fact]
    public async Task Materialise_WritesTheChargeWithTheScheduleAmount()
    {
        var manager = NewManager(out _, out var charges);
        var schedule = await manager.CreateAsync(Rent());

        var charge = await manager.MaterialiseAsync(schedule.ScheduleId, Jan3);

        Assert.NotNull(charge);
        Assert.Equal(1000m, charge!.Amount.Amount);
        Assert.Equal(Jan3, charge.OccurrenceDate);
        Assert.Single(charges.Saved);
    }

    [Fact]
    public async Task Materialise_IsIdempotent()
    {
        var manager = NewManager(out _, out var charges);
        var schedule = await manager.CreateAsync(Rent());

        var first = await manager.MaterialiseAsync(schedule.ScheduleId, Jan3);
        var second = await manager.MaterialiseAsync(schedule.ScheduleId, Jan3);

        Assert.Equal(first!.Id, second!.Id);
        Assert.Single(charges.Saved);
    }

    [Fact]
    public async Task Materialise_RefusesADateTheScheduleDoesNotPlaceACharge()
    {
        var manager = NewManager(out _, out _);
        var schedule = await manager.CreateAsync(Rent());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => manager.MaterialiseAsync(schedule.ScheduleId, Jan3.AddDays(5)));
    }

    [Fact]
    public async Task AmendingAfterMaterialising_LeavesTheRecordedMonthAlone()
    {
        var manager = NewManager(out _, out _);
        var schedule = await manager.CreateAsync(Rent());
        var january = await manager.MaterialiseAsync(schedule.ScheduleId, Jan3);

        await manager.AmendAsync(new AmendChargeScheduleCommand(
            schedule.ScheduleId, User, "Rent", 1100m, "USD", ChargeCategory.Rent,
            EffectiveFrom: Jan3.AddMonths(1)));

        var feb = await manager.MaterialiseAsync(schedule.ScheduleId, Jan3.AddMonths(1));

        Assert.Equal(1000m, january!.Amount.Amount);
        Assert.Equal(1100m, feb!.Amount.Amount);
    }

    [Fact]
    public async Task Forecast_ReportsWhatWasBilled_ForRecordedMonthsAndTheScheduleForTherest()
    {
        var manager = NewManager(out _, out _);
        var schedule = await manager.CreateAsync(Rent());
        await manager.MaterialiseAsync(schedule.ScheduleId, Jan3);
        await manager.AmendAsync(new AmendChargeScheduleCommand(
            schedule.ScheduleId, User, "Rent", 1100m, "USD", ChargeCategory.Rent,
            EffectiveFrom: Jan3.AddMonths(1)));

        var forecast = await manager.ForecastAsync(schedule.ScheduleId, Jan3, Jan3.AddMonths(3));

        Assert.Equal(3, forecast.Count);
        // January was billed at 1,000 and says so; the months not yet recorded quote the schedule.
        Assert.Equal(1000m, forecast[0].Amount);
        Assert.NotNull(forecast[0].ChargeId);
        Assert.Equal(1100m, forecast[1].Amount);
        Assert.Null(forecast[1].ChargeId);
    }

    [Fact]
    public async Task Deactivate_StopsFutureOccurrencesAndKeepsWhatWasRecorded()
    {
        var manager = NewManager(out _, out var charges);
        var schedule = await manager.CreateAsync(Rent());
        await manager.MaterialiseAsync(schedule.ScheduleId, Jan3);

        await manager.DeactivateAsync(schedule.ScheduleId, User);

        Assert.Empty(await manager.ForecastAsync(schedule.ScheduleId, Jan3, Jan3.AddMonths(6)));
        Assert.Single(charges.Saved);
    }

    [Fact]
    public async Task CatchUp_WritesEveryPeriodThatHasPassed()
    {
        var manager = NewManager(out _, out var charges);
        var schedule = await manager.CreateAsync(Rent());

        // Three months on: January, February and March have all come due.
        var written = await manager.CatchUpAsync(Group, User, Jan3.AddMonths(2));

        Assert.Equal(3, written);
        Assert.Equal(
            [Jan3, Jan3.AddMonths(1), Jan3.AddMonths(2)],
            charges.Saved.Select(c => c.OccurrenceDate).OrderBy(d => d));
    }

    [Fact]
    public async Task CatchUp_StopsAtToday_SoNothingUnhappenedIsOnTheBooks()
    {
        var manager = NewManager(out _, out var charges);
        var schedule = await manager.CreateAsync(Rent());

        await manager.CatchUpAsync(Group, User, Jan3.AddDays(20));

        // February's rent has not happened. Writing it would put a cost in the books that nobody
        // has incurred.
        Assert.Single(charges.Saved);
        Assert.Equal(Jan3, charges.Saved[0].OccurrenceDate);
    }

    [Fact]
    public async Task CatchUp_IsIdempotent_SoTwoLoadsDoNotDoubleBill()
    {
        var manager = NewManager(out _, out var charges);
        await manager.CreateAsync(Rent());

        await manager.CatchUpAsync(Group, User, Jan3.AddMonths(2));
        var second = await manager.CatchUpAsync(Group, User, Jan3.AddMonths(2));

        Assert.Equal(0, second);
        Assert.Equal(3, charges.Saved.Count);
    }

    [Fact]
    public async Task CatchUp_BillsEachPeriodAtWhatWasAgreedThen()
    {
        var manager = NewManager(out _, out var charges);
        var schedule = await manager.CreateAsync(Rent());
        await manager.AmendAsync(new AmendChargeScheduleCommand(
            schedule.ScheduleId, User, "Rent", 1100m, "USD", ChargeCategory.Rent,
            EffectiveFrom: Jan3.AddMonths(2)));

        await manager.CatchUpAsync(Group, User, Jan3.AddMonths(2));

        var byDate = charges.Saved.ToDictionary(c => c.OccurrenceDate, c => c.Amount.Amount);
        Assert.Equal(1000m, byDate[Jan3]);
        Assert.Equal(1000m, byDate[Jan3.AddMonths(1)]);
        Assert.Equal(1100m, byDate[Jan3.AddMonths(2)]);
    }

    [Fact]
    public async Task CatchUp_SkipsADeactivatedSchedule()
    {
        var manager = NewManager(out _, out var charges);
        var schedule = await manager.CreateAsync(Rent());
        await manager.DeactivateAsync(schedule.ScheduleId, User);

        Assert.Equal(0, await manager.CatchUpAsync(Group, User, Jan3.AddMonths(3)));
        Assert.Empty(charges.Saved);
    }

    internal sealed class FakeScheduleRepo : IChargeScheduleRepository
    {
        public List<ChargeSchedule> Schedules { get; } = new();
        public List<Charge> Charges { get; } = new();

        public Task AddAsync(ChargeSchedule s, CancellationToken ct = default) { Schedules.Add(s); return Task.CompletedTask; }
        public Task<ChargeSchedule?> GetByIdAsync(ChargeScheduleId id, CancellationToken ct = default)
            => Task.FromResult(Schedules.FirstOrDefault(s => s.Id == id));
        public Task<IReadOnlyList<ChargeSchedule>> ListForGroupAsync(GroupId g, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ChargeSchedule>>(Schedules.Where(s => s.GroupId == g).ToList());
        public Task<IReadOnlyList<ChargeSchedule>> ListForUserAsync(UserId u, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ChargeSchedule>>(Schedules.Where(s => s.CreatedBy == u && s.GroupId == null).ToList());
        public Task<Charge?> GetGeneratedAsync(ChargeScheduleId id, DateTime date, CancellationToken ct = default)
            => Task.FromResult(Charges.FirstOrDefault(c => c.ScheduleId == id && c.OccurrenceDate == date.Date));
        public Task<IReadOnlyDictionary<DateTime, Charge>> ListGeneratedAsync(
            ChargeScheduleId id, DateTime from, DateTime to, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<DateTime, Charge>>(
                Charges.Where(c => c.ScheduleId == id && c.OccurrenceDate >= from.Date && c.OccurrenceDate <= to.Date)
                       .ToDictionary(c => c.OccurrenceDate));
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    internal sealed class FakeChargeRepo(FakeScheduleRepo shared) : IChargeRepository
    {
        public List<Charge> Saved => shared.Charges;

        public Task AddAsync(Charge c, CancellationToken ct = default) { shared.Charges.Add(c); return Task.CompletedTask; }
        public Task UpdateAsync(Charge c, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(Charge c, CancellationToken ct = default) { shared.Charges.Remove(c); return Task.CompletedTask; }
        public Task<Charge?> GetByIdAsync(ChargeId id, CancellationToken ct = default)
            => Task.FromResult(shared.Charges.FirstOrDefault(c => c.Id == id));
        public Task<IReadOnlyList<Charge>> ListUnpostedPersonalAsync(UserId userId, DateTime asOf, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Charge>>(
                shared.Charges.Where(c => c.Owner == AccountingEntity.Person(userId) && c.IsActive && c.OccurrenceDate.Date <= asOf.Date).ToList());
        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAllForUserAsync(UserId u, CancellationToken ct = default) => Task.CompletedTask;
    }
}
