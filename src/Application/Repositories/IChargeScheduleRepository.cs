using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Repositories;

/// <summary>Persistence only — amending and deactivating happen on the aggregate.</summary>
public interface IChargeScheduleRepository
{
    Task AddAsync(ChargeSchedule schedule, CancellationToken ct = default);
    Task<ChargeSchedule?> GetByIdAsync(ChargeScheduleId id, CancellationToken ct = default);
    Task<IReadOnlyList<ChargeSchedule>> ListForGroupAsync(GroupId groupId, CancellationToken ct = default);
    Task<IReadOnlyList<ChargeSchedule>> ListForUserAsync(UserId userId, CancellationToken ct = default);

    /// <summary>The charge already generated for that occurrence, or null. Keyed the same way the
    /// unique index is, so "has this month been recorded" is one lookup.</summary>
    Task<Charge?> GetGeneratedAsync(ChargeScheduleId scheduleId, DateTime occurrenceDate, CancellationToken ct = default);

    Task CommitAsync(CancellationToken ct = default);
}
