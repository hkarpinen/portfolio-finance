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
        if (await db.ProcessedEvents.AnyAsync(e => e.EventId == message.Id, context.CancellationToken))
            return;

        var userId = new UserId(message.UserId);
        var nameParts = (message.DisplayName ?? "").Split(' ', 2);

        // Infrastructure concern: create or update user projection
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

        // Application concern: seed domain aggregates via manager
        await demoSeedManager.SeedAsync(message.UserId, context.CancellationToken);

        db.ProcessedEvents.Add(new ProcessedEvent(message.Id, nameof(DemoUserCreated), DateTime.UtcNow));

        try
        {
            await db.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { }
    }
}

internal sealed class DemoHouseholdSeededConsumer(
    FinanceDbContext db,
    IDemoSeedManager demoSeedManager) : IConsumer<DemoHouseholdSeededEvent>
{
    public async Task Consume(ConsumeContext<DemoHouseholdSeededEvent> context)
    {
        var message = context.Message;
        if (await db.ProcessedEvents.AnyAsync(e => e.EventId == message.Id, context.CancellationToken))
            return;

        await demoSeedManager.SeedGroupChargesAsync(message.UserId, message.HouseholdId, context.CancellationToken);

        db.ProcessedEvents.Add(new ProcessedEvent(message.Id, nameof(DemoHouseholdSeededEvent), DateTime.UtcNow));

        try
        {
            await db.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { }
    }
}

internal sealed class DemoUserExpiredConsumer(
    FinanceDbContext db,
    IDemoSeedManager demoSeedManager) : IConsumer<DemoUserExpired>
{
    public async Task Consume(ConsumeContext<DemoUserExpired> context)
    {
        var message = context.Message;
        if (await db.ProcessedEvents.AnyAsync(e => e.EventId == message.Id, context.CancellationToken))
            return;

        var userId = new UserId(message.UserId);

        // Application concern: delete domain aggregates via manager
        await demoSeedManager.CleanupAsync(message.UserId, context.CancellationToken);

        // Infrastructure concern: remove user projection
        var projection = await db.UserProjections
            .FirstOrDefaultAsync(u => u.UserId == userId, context.CancellationToken);
        if (projection is not null)
            db.UserProjections.Remove(projection);

        db.ProcessedEvents.Add(new ProcessedEvent(message.Id, nameof(DemoUserExpired), DateTime.UtcNow));

        try
        {
            await db.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { }
    }
}
