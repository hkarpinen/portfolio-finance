using Finance.Application.Managers.Demo;
using Finance.Domain.ValueObjects;
using Finance.Infrastructure.Persistence.Projections;
using Domain.Events;
using Infrastructure.Messaging.Events;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Messaging.Consumers;

internal sealed class DemoUserCreatedConsumer(
    FinanceDbContext db,
    IDemoSeedManager demoSeedManager) : IConsumer<DemoUserCreated>
{
    public async Task Consume(ConsumeContext<DemoUserCreated> context)
    {
        var message = context.Message;

        var userId = new UserId(message.UserId);
        var nameParts = (message.DisplayName ?? "").Split(' ', 2);

        var existing = await db.UserProjections
            .FirstOrDefaultAsync(u => u.UserId == userId, context.CancellationToken);

        if (existing is null)
        {
            var projection = UserProjection.Create(
                userId,
                message.Email,
                nameParts.Length > 0 ? nameParts[0] : "Demo",
                nameParts.Length > 1 ? nameParts[1] : "User");
            projection.IsDemo = true;
            await db.UserProjections.AddAsync(projection, context.CancellationToken);
        }
        else
        {
            existing.IsDemo = true;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await demoSeedManager.SeedAsync(message.UserId, context.CancellationToken);

        await db.SaveChangesAsync(context.CancellationToken);
    }
}

internal sealed class DemoHouseholdSeededConsumer(
    FinanceDbContext db,
    IDemoSeedManager demoSeedManager) : IConsumer<DemoHouseholdSeededEvent>
{
    public async Task Consume(ConsumeContext<DemoHouseholdSeededEvent> context)
    {
        var message = context.Message;

        await demoSeedManager.SeedGroupExpensesAsync(message.UserId, message.HouseholdId, context.CancellationToken);

        await db.SaveChangesAsync(context.CancellationToken);
    }
}

internal sealed class DemoUserExpiredConsumer(
    FinanceDbContext db,
    IDemoSeedManager demoSeedManager) : IConsumer<DemoUserExpired>
{
    public async Task Consume(ConsumeContext<DemoUserExpired> context)
    {
        var message = context.Message;

        var userId = new UserId(message.UserId);

        await demoSeedManager.CleanupAsync(message.UserId, context.CancellationToken);

        var projection = await db.UserProjections
            .FirstOrDefaultAsync(u => u.UserId == userId, context.CancellationToken);
        if (projection is not null)
            db.UserProjections.Remove(projection);

        await db.SaveChangesAsync(context.CancellationToken);
    }
}
