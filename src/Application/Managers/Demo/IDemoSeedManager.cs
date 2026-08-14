namespace Finance.Application.Managers.Demo;

public interface IDemoSeedManager
{
    Task SeedAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SeedGroupExpensesAsync(Guid userId, Guid groupId, CancellationToken cancellationToken = default);
    Task CleanupAsync(Guid userId, CancellationToken cancellationToken = default);
}
