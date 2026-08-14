using Finance.Application.Dtos;
using Finance.Application.Mappers;
using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Managers;

public interface IChargeScheduleManager
{
    Task<ChargeScheduleDto> CreateAsync(CreateChargeScheduleCommand command, CancellationToken ct = default);
    Task<ChargeScheduleDto?> AmendAsync(AmendChargeScheduleCommand command, CancellationToken ct = default);
    Task<bool> DeactivateAsync(Guid scheduleId, Guid callerUserId, CancellationToken ct = default);
    Task<IReadOnlyList<ChargeScheduleDto>> ListForGroupAsync(Guid groupId, CancellationToken ct = default);
    Task<IReadOnlyList<ChargeScheduleDto>> ListForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Dates in a window with the charge that exists for each, or null where none does.</summary>
    Task<IReadOnlyList<ScheduledOccurrenceDto>> ForecastAsync(
        Guid scheduleId, DateTime from, DateTime toExclusive, CancellationToken ct = default);

    /// <summary>
    /// Writes the charge for one occurrence if it is not already there, and returns it either way.
    ///
    /// This is the generation step, and it is deliberately driven by somebody DOING something —
    /// paying a share, marking the vendor paid — rather than by a clock. Nothing needs a charge to
    /// exist until then, and generating ahead of time would put a cost in the books that has not
    /// happened.
    /// </summary>
    Task<Charge?> MaterialiseAsync(Guid scheduleId, DateTime occurrenceDate, CancellationToken ct = default);

    /// <summary>
    /// Writes every occurrence that has come due and is not on the books yet, for one house or one
    /// person, and returns how many that was.
    ///
    /// This is how a period passing turns into a charge without a clock: nobody needs the bill to
    /// exist until somebody looks at the money, and by the time they do, it does. Never writes
    /// past <paramref name="asOf"/> — a cost that has not happened does not belong in the books.
    /// </summary>
    Task<int> CatchUpAsync(Guid? groupId, Guid userId, DateTime asOf, CancellationToken ct = default);
}

internal sealed class ChargeScheduleManager(
    IChargeScheduleRepository schedules,
    IChargeRepository charges) : IChargeScheduleManager
{
    public async Task<ChargeScheduleDto> CreateAsync(CreateChargeScheduleCommand cmd, CancellationToken ct = default)
    {
        var schedule = ChargeSchedule.Create(
            cmd.GroupId is { } g ? AccountingEntity.Household(g) : AccountingEntity.Person(cmd.CallerUserId),
            UserId.Create(cmd.CallerUserId),
            cmd.Title,
            Money.Create(cmd.Amount, cmd.Currency),
            cmd.Category,
            RecurrenceSchedule.Create(cmd.Frequency, cmd.AnchorDate, cmd.EndDate),
            cmd.Description,
            cmd.PayerUserId,
            cmd.FundingSource);

        await schedules.AddAsync(schedule, ct);
        await schedules.CommitAsync(ct);
        return ChargeScheduleMapper.ToResponse(schedule);
    }

    public async Task<ChargeScheduleDto?> AmendAsync(AmendChargeScheduleCommand cmd, CancellationToken ct = default)
    {
        var schedule = await schedules.GetByIdAsync(ChargeScheduleId.Create(cmd.ScheduleId), ct);
        if (schedule is null) return null;

        // Takes effect on occurrences not yet generated. Charges already written keep their own
        // amount, which is the entire reason the two are separate.
        schedule.Amend(
            cmd.Title, Money.Create(cmd.Amount, cmd.Currency), cmd.Category, cmd.Description,
            cmd.EffectiveFrom);
        await schedules.CommitAsync(ct);
        return ChargeScheduleMapper.ToResponse(schedule);
    }

    public async Task<bool> DeactivateAsync(Guid scheduleId, Guid callerUserId, CancellationToken ct = default)
    {
        var schedule = await schedules.GetByIdAsync(ChargeScheduleId.Create(scheduleId), ct);
        if (schedule is null) return false;

        // Stops future occurrences only. Charges already generated are history and stay.
        schedule.Deactivate();
        await schedules.CommitAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ChargeScheduleDto>> ListForGroupAsync(Guid groupId, CancellationToken ct = default)
        => (await schedules.ListForGroupAsync(GroupId.Create(groupId), ct)).Select(ChargeScheduleMapper.ToResponse).ToList();

    public async Task<IReadOnlyList<ChargeScheduleDto>> ListForUserAsync(Guid userId, CancellationToken ct = default)
        => (await schedules.ListForUserAsync(UserId.Create(userId), ct)).Select(ChargeScheduleMapper.ToResponse).ToList();

    public async Task<IReadOnlyList<ScheduledOccurrenceDto>> ForecastAsync(
        Guid scheduleId, DateTime from, DateTime toExclusive, CancellationToken ct = default)
    {
        var schedule = await schedules.GetByIdAsync(ChargeScheduleId.Create(scheduleId), ct);
        if (schedule is null) return [];

        var dates = schedule.OccurrencesIn(from, toExclusive);
        if (dates.Count == 0) return [];

        // One query for the window rather than one per date — a daily schedule over a year is 365
        // occurrences, and a lookup each would be 365 round trips.
        var recorded = await schedules.ListGeneratedAsync(schedule.Id, dates[0], dates[^1], ct);

        return dates
            .Select(d => ChargeScheduleMapper.ToOccurrence(
                schedule, d, recorded.GetValueOrDefault(d)))
            .ToList();
    }

    public async Task<int> CatchUpAsync(Guid? groupId, Guid userId, DateTime asOf, CancellationToken ct = default)
    {
        var due = DateTime.SpecifyKind(asOf.Date, DateTimeKind.Utc);
        var active = groupId is { } g
            ? await schedules.ListForGroupAsync(GroupId.Create(g), ct)
            : await schedules.ListForUserAsync(UserId.Create(userId), ct);

        var written = 0;
        foreach (var schedule in active)
        {
            // Inclusive of today: a bill due this morning has come due.
            var dates = schedule.OccurrencesIn(schedule.Recurrence.StartDate, due.AddDays(1));
            if (dates.Count == 0) continue;

            var already = await schedules.ListGeneratedAsync(schedule.Id, dates[0], dates[^1], ct);

            foreach (var date in dates)
            {
                if (already.ContainsKey(date)) continue;
                await charges.AddAsync(Charge.GenerateFrom(schedule, date), ct);
                written++;
            }
        }

        if (written > 0) await charges.CommitAsync(ct);
        return written;
    }

    public async Task<Charge?> MaterialiseAsync(Guid scheduleId, DateTime occurrenceDate, CancellationToken ct = default)
    {
        var schedule = await schedules.GetByIdAsync(ChargeScheduleId.Create(scheduleId), ct);
        if (schedule is null) return null;

        var day = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc);

        var existing = await schedules.GetGeneratedAsync(schedule.Id, day, ct);
        if (existing is not null) return existing;

        // Throws when the schedule places no charge on that day, so a caller cannot invent an
        // occurrence the agreement never described.
        var charge = Charge.GenerateFrom(schedule, day);
        await charges.AddAsync(charge, ct);
        await charges.CommitAsync(ct);
        return charge;
    }

}
