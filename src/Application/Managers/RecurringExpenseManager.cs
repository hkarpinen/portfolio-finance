using Finance.Application.Dtos;
using Finance.Application.Mappers;
using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Managers;

public interface IRecurringExpenseManager
{
    Task<RecurringExpenseDto> CreateAsync(CreateRecurringExpenseCommand command, CancellationToken ct = default);
    Task<RecurringExpenseDto?> AmendAsync(AmendRecurringExpenseCommand command, CancellationToken ct = default);
    Task<bool> DeactivateAsync(Guid recurringExpenseId, Guid callerUserId, CancellationToken ct = default);
    Task<IReadOnlyList<RecurringExpenseDto>> ListForGroupAsync(Guid groupId, CancellationToken ct = default);
    Task<IReadOnlyList<RecurringExpenseDto>> ListForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Dates in a window with the expense that exists for each, or null where none does.</summary>
    Task<IReadOnlyList<ScheduledOccurrenceDto>> ForecastAsync(
        Guid recurringExpenseId, DateTime from, DateTime toExclusive, CancellationToken ct = default);

    /// <summary>
    /// Writes the expense for one occurrence if it is not already there, and returns it either way.
    ///
    /// This is the generation step, and it is deliberately driven by somebody DOING something —
    /// paying a share, marking the vendor paid — rather than by a clock. Nothing needs an expense to
    /// exist until then, and generating ahead of time would put a cost in the books that has not
    /// happened.
    /// </summary>
    Task<Expense?> MaterialiseAsync(Guid recurringExpenseId, DateTime occurrenceDate, CancellationToken ct = default);

    /// <summary>
    /// Writes every occurrence that has come due and is not on the books yet, for one house or one
    /// person, and returns how many that was.
    ///
    /// This is how a period passing turns into an expense without a clock: nobody needs the bill to
    /// exist until somebody looks at the money, and by the time they do, it does. Never writes
    /// past <paramref name="asOf"/> — a cost that has not happened does not belong in the books.
    /// </summary>
    Task<int> CatchUpAsync(Guid? groupId, Guid userId, DateTime asOf, CancellationToken ct = default);
}

internal sealed class RecurringExpenseManager(
    IRecurringExpenseRepository schedules,
    IExpenseRepository expenses) : IRecurringExpenseManager
{
    public async Task<RecurringExpenseDto> CreateAsync(CreateRecurringExpenseCommand cmd, CancellationToken ct = default)
    {
        var schedule = RecurringExpense.Create(
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
        return RecurringExpenseMapper.ToResponse(schedule);
    }

    public async Task<RecurringExpenseDto?> AmendAsync(AmendRecurringExpenseCommand cmd, CancellationToken ct = default)
    {
        var schedule = await schedules.GetByIdAsync(RecurringExpenseId.Create(cmd.RecurringExpenseId), ct);
        if (schedule is null) return null;

        // Takes effect on occurrences not yet generated. Expenses already written keep their own
        // amount, which is the entire reason the two are separate.
        schedule.Amend(
            cmd.Title, Money.Create(cmd.Amount, cmd.Currency), cmd.Category, cmd.Description,
            cmd.EffectiveFrom);
        await schedules.CommitAsync(ct);
        return RecurringExpenseMapper.ToResponse(schedule);
    }

    public async Task<bool> DeactivateAsync(Guid recurringExpenseId, Guid callerUserId, CancellationToken ct = default)
    {
        var schedule = await schedules.GetByIdAsync(RecurringExpenseId.Create(recurringExpenseId), ct);
        if (schedule is null) return false;

        // Stops future occurrences only. Expenses already generated are history and stay.
        schedule.Deactivate();
        await schedules.CommitAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<RecurringExpenseDto>> ListForGroupAsync(Guid groupId, CancellationToken ct = default)
        => (await schedules.ListForGroupAsync(GroupId.Create(groupId), ct)).Select(RecurringExpenseMapper.ToResponse).ToList();

    public async Task<IReadOnlyList<RecurringExpenseDto>> ListForUserAsync(Guid userId, CancellationToken ct = default)
        => (await schedules.ListForUserAsync(UserId.Create(userId), ct)).Select(RecurringExpenseMapper.ToResponse).ToList();

    public async Task<IReadOnlyList<ScheduledOccurrenceDto>> ForecastAsync(
        Guid recurringExpenseId, DateTime from, DateTime toExclusive, CancellationToken ct = default)
    {
        var schedule = await schedules.GetByIdAsync(RecurringExpenseId.Create(recurringExpenseId), ct);
        if (schedule is null) return [];

        var dates = schedule.OccurrencesIn(from, toExclusive);
        if (dates.Count == 0) return [];

        // One query for the window rather than one per date — a daily schedule over a year is 365
        // occurrences, and a lookup each would be 365 round trips.
        var recorded = await schedules.ListGeneratedAsync(schedule.Id, dates[0], dates[^1], ct);

        return dates
            .Select(d => RecurringExpenseMapper.ToOccurrence(
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
                await expenses.AddAsync(Expense.GenerateFrom(schedule, date), ct);
                written++;
            }
        }

        if (written > 0) await expenses.CommitAsync(ct);
        return written;
    }

    public async Task<Expense?> MaterialiseAsync(Guid recurringExpenseId, DateTime occurrenceDate, CancellationToken ct = default)
    {
        var schedule = await schedules.GetByIdAsync(RecurringExpenseId.Create(recurringExpenseId), ct);
        if (schedule is null) return null;

        var day = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc);

        var existing = await schedules.GetGeneratedAsync(schedule.Id, day, ct);
        if (existing is not null) return existing;

        // Throws when the schedule places no expense on that day, so a caller cannot invent an
        // occurrence the agreement never described.
        var expense = Expense.GenerateFrom(schedule, day);
        await expenses.AddAsync(expense, ct);
        await expenses.CommitAsync(ct);
        return expense;
    }

}
