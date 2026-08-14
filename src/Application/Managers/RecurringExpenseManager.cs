using Finance.Application.Dtos;
using Finance.Application.Mappers;
using Finance.Application.Queries;
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
        Guid recurringExpenseId, Guid callerUserId, DateTime from, DateTime toExclusive,
        CancellationToken ct = default);

    /// <summary>
    /// Writes the expense for one occurrence if it is not already there, and returns it either way.
    ///
    /// This is the generation step, and it is deliberately driven by somebody DOING something —
    /// paying a share, marking the vendor paid — rather than by a clock. Nothing needs an expense to
    /// exist until then, and generating ahead of time would put a cost in the books that has not
    /// happened.
    /// </summary>
    Task<Expense?> MaterialiseAsync(
        Guid recurringExpenseId, Guid callerUserId, DateTime occurrenceDate, CancellationToken ct = default);

    /// <summary>
    /// Writes every occurrence that has come due and is not on the books yet, for one group or one
    /// person, and returns how many that was.
    ///
    /// This is how a period passing turns into an expense without a clock: nobody needs the bill to
    /// exist until somebody looks at the money, and by the time they do, it does. Never writes
    /// past <paramref name="asOf"/> — a cost that has not happened does not belong in the books.
    /// </summary>
    Task<int> CatchUpAsync(Guid? groupId, Guid userId, DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// Opens the agreement and bills the occurrence the caller was entering, so somebody adding a
    /// repeating cost gets an expense back exactly as if they had entered a one-off — the agreement
    /// sits behind it, and every later period comes from it.
    /// </summary>
    Task<Expense> OpenAndBillFirstAsync(CreateRecurringExpenseCommand cmd, CancellationToken ct = default);

    /// <summary>
    /// Both halves of one person's catch-up: write the bills whose period has come round, then post
    /// anything now due — including a one-off entered ahead of its date. Paired here so the
    /// controller does not have to know that the second half is bookkeeping.
    /// </summary>
    Task<(int Generated, int Posted)> CatchUpPersonalAsync(Guid userId, DateTime asOf, CancellationToken ct = default);
}

internal sealed class RecurringExpenseManager(
    IRecurringExpenseRepository schedules,
    IExpenseRepository expenses,
    IGroupQuery groups,
    IBookkeepingManager bookkeeping) : IRecurringExpenseManager
{
    public async Task<RecurringExpenseDto> CreateAsync(CreateRecurringExpenseCommand cmd, CancellationToken ct = default)
    {
        // The group is named by the body — this route has no {groupId} for the membership
        // filter to read — so the one check standing between a signed-in stranger and a standing
        // charge on somebody else's group is this one.
        if (cmd.GroupId is { } named && !await groups.IsCurrentMemberAsync(named, cmd.CallerUserId, ct))
            throw new UnauthorizedAccessException("Access denied.");

        var schedule = RecurringExpense.Create(
            cmd.GroupId is { } g ? AccountingEntity.Group(g) : AccountingEntity.Person(cmd.CallerUserId),
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

        // Same answer as "no such schedule": a 403 would confirm the id exists.
        if (!schedule.IsManagedBy(cmd.CallerUserId)) return null;

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
        if (!schedule.IsManagedBy(callerUserId)) return false;

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
        Guid recurringExpenseId, Guid callerUserId, DateTime from, DateTime toExclusive,
        CancellationToken ct = default)
    {
        var schedule = await schedules.GetByIdAsync(RecurringExpenseId.Create(recurringExpenseId), ct);
        if (schedule is null) return [];

        // A forecast states what the agreement charges and when — reading it is reading the terms.
        var isMember = schedule.GroupId is { } g
            && await groups.IsCurrentMemberAsync(g.Value, callerUserId, ct);
        if (!schedule.IsVisibleTo(callerUserId, isMember)) return [];

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

    public async Task<(int Generated, int Posted)> CatchUpPersonalAsync(
        Guid userId, DateTime asOf, CancellationToken ct = default)
    {
        var generated = await CatchUpAsync(null, userId, asOf, ct);
        var posted = await bookkeeping.PostDuePersonalExpensesAsync(userId, asOf, ct);
        return (generated, posted);
    }

    public async Task<Expense> OpenAndBillFirstAsync(
        CreateRecurringExpenseCommand cmd, CancellationToken ct = default)
    {
        var schedule = await CreateAsync(cmd, ct);

        return await MaterialiseAsync(schedule.RecurringExpenseId, cmd.CallerUserId, cmd.AnchorDate, ct)
            ?? throw new InvalidOperationException("The agreement was opened but its first bill was not written.");
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

    public async Task<Expense?> MaterialiseAsync(
        Guid recurringExpenseId, Guid callerUserId, DateTime occurrenceDate, CancellationToken ct = default)
    {
        var schedule = await schedules.GetByIdAsync(RecurringExpenseId.Create(recurringExpenseId), ct);
        if (schedule is null) return null;

        // Writing a bill into somebody's books. Anyone who can see the agreement may bill a period
        // that has come round — that is how paying a share generates the expense — but a stranger
        // must not be able to put a cost on a group they are not in.
        var isMember = schedule.GroupId is { } g
            && await groups.IsCurrentMemberAsync(g.Value, callerUserId, ct);
        if (!schedule.IsVisibleTo(callerUserId, isMember)) return null;

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
