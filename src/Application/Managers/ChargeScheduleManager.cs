using Finance.Application.Dtos;
using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Managers;

public interface IChargeScheduleManager
{
    Task<ChargeScheduleDto> CreateAsync(CreateChargeScheduleCommand command, CancellationToken ct = default);
    Task<ChargeScheduleDto?> AmendAsync(AmendChargeScheduleCommand command, CancellationToken ct = default);
    Task<bool> DeactivateAsync(Guid scheduleId, Guid callerId, CancellationToken ct = default);
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
}

internal sealed class ChargeScheduleManager(
    IChargeScheduleRepository schedules,
    IChargeRepository charges) : IChargeScheduleManager
{
    public async Task<ChargeScheduleDto> CreateAsync(CreateChargeScheduleCommand cmd, CancellationToken ct = default)
    {
        var schedule = ChargeSchedule.Create(
            UserId.Create(cmd.UserId),
            cmd.GroupId is { } g ? GroupId.Create(g) : null,
            cmd.Title,
            Money.Create(cmd.Amount, cmd.Currency),
            cmd.Category,
            RecurrenceSchedule.Create(cmd.Frequency, cmd.AnchorDate, cmd.EndDate),
            cmd.Description,
            UserId.Create(cmd.UserId),
            cmd.PayerUserId,
            cmd.FundingSource);

        await schedules.AddAsync(schedule, ct);
        await schedules.CommitAsync(ct);
        return Map(schedule);
    }

    public async Task<ChargeScheduleDto?> AmendAsync(AmendChargeScheduleCommand cmd, CancellationToken ct = default)
    {
        var schedule = await schedules.GetByIdAsync(ChargeScheduleId.Create(cmd.ScheduleId), ct);
        if (schedule is null) return null;

        // Takes effect on occurrences not yet generated. Charges already written keep their own
        // amount, which is the entire reason the two are separate.
        schedule.Amend(cmd.Title, Money.Create(cmd.Amount, cmd.Currency), cmd.Category, cmd.Description);
        await schedules.CommitAsync(ct);
        return Map(schedule);
    }

    public async Task<bool> DeactivateAsync(Guid scheduleId, Guid callerId, CancellationToken ct = default)
    {
        var schedule = await schedules.GetByIdAsync(ChargeScheduleId.Create(scheduleId), ct);
        if (schedule is null) return false;

        // Stops future occurrences only. Charges already generated are history and stay.
        schedule.Deactivate();
        await schedules.CommitAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ChargeScheduleDto>> ListForGroupAsync(Guid groupId, CancellationToken ct = default)
        => (await schedules.ListForGroupAsync(GroupId.Create(groupId), ct)).Select(Map).ToList();

    public async Task<IReadOnlyList<ChargeScheduleDto>> ListForUserAsync(Guid userId, CancellationToken ct = default)
        => (await schedules.ListForUserAsync(UserId.Create(userId), ct)).Select(Map).ToList();

    public async Task<IReadOnlyList<ScheduledOccurrenceDto>> ForecastAsync(
        Guid scheduleId, DateTime from, DateTime toExclusive, CancellationToken ct = default)
    {
        var schedule = await schedules.GetByIdAsync(ChargeScheduleId.Create(scheduleId), ct);
        if (schedule is null) return [];

        var results = new List<ScheduledOccurrenceDto>();
        foreach (var date in schedule.OccurrencesIn(from, toExclusive))
        {
            var existing = await schedules.GetGeneratedAsync(schedule.Id, date, ct);
            results.Add(new ScheduledOccurrenceDto(
                date,
                // A recorded occurrence reports what it was billed at, not what the schedule says
                // today — that difference is the whole point of freezing it.
                existing?.Amount.Amount ?? schedule.Amount.Amount,
                existing?.Amount.Currency ?? schedule.Amount.Currency,
                existing?.Id.Value));
        }
        return results;
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

    private static ChargeScheduleDto Map(ChargeSchedule s) => new(
        s.Id.Value, s.GroupId?.Value, s.Title, s.Description,
        s.Amount.Amount, s.Amount.Currency, s.Category.ToString(),
        s.Recurrence.Frequency.ToString(), s.Recurrence.StartDate, s.Recurrence.EndDate,
        s.PayerUserId, s.FundingSource.ToString(), s.IsActive);
}
