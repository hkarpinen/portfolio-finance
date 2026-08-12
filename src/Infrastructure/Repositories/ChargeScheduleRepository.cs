using Finance.Application.Repositories;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

internal sealed class ChargeScheduleRepository(FinanceDbContext db) : IChargeScheduleRepository
{
    public async Task AddAsync(ChargeSchedule schedule, CancellationToken ct = default)
        => await db.ChargeSchedules.AddAsync(schedule, ct);

    public Task<ChargeSchedule?> GetByIdAsync(ChargeScheduleId id, CancellationToken ct = default)
        => db.ChargeSchedules.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<IReadOnlyList<ChargeSchedule>> ListForGroupAsync(GroupId groupId, CancellationToken ct = default)
        => await db.ChargeSchedules.AsNoTracking()
            .Where(s => s.GroupId == groupId && s.IsActive)
            .OrderBy(s => s.Title)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ChargeSchedule>> ListForUserAsync(UserId userId, CancellationToken ct = default)
        => await db.ChargeSchedules.AsNoTracking()
            .Where(s => s.UserId == userId && s.GroupId == null && s.IsActive)
            .OrderBy(s => s.Title)
            .ToListAsync(ct);

    public Task<Charge?> GetGeneratedAsync(ChargeScheduleId scheduleId, DateTime occurrenceDate, CancellationToken ct = default)
    {
        var day = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc);
        return db.Charges.FirstOrDefaultAsync(
            c => c.ScheduleId == scheduleId && c.OccurrenceDate == day, ct);
    }

    public async Task<IReadOnlyDictionary<DateTime, Charge>> ListGeneratedAsync(
        ChargeScheduleId scheduleId, DateTime from, DateTime to, CancellationToken ct = default)
    {
        var first = DateTime.SpecifyKind(from.Date, DateTimeKind.Utc);
        var last = DateTime.SpecifyKind(to.Date, DateTimeKind.Utc);

        var rows = await db.Charges.AsNoTracking()
            .Where(c => c.ScheduleId == scheduleId && c.OccurrenceDate >= first && c.OccurrenceDate <= last)
            .ToListAsync(ct);

        return rows.ToDictionary(c => c.OccurrenceDate);
    }

    public Task CommitAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
