using Bills.Domain.ReadModels;
using Bills.Domain.ValueObjects;
using Infrastructure.Messaging.Events;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Messaging.Consumers;

internal sealed class UserBannedConsumer : IConsumer<UserBannedEvent>
{
    private readonly BillsDbContext _dbContext;

    public UserBannedConsumer(BillsDbContext dbContext) => _dbContext = dbContext;

    public async Task Consume(ConsumeContext<UserBannedEvent> context)
    {
        var message = context.Message;
        if (await _dbContext.ProcessedEvents.AnyAsync(e => e.EventId == message.Id, context.CancellationToken))
            return;

        var userId = new UserId(message.UserId);
        var existing = await _dbContext.UserProjections
            .FirstOrDefaultAsync(u => u.UserId == userId, context.CancellationToken);

        if (existing is not null)
        {
            existing.IsActive  = false;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            // UserRegistered was not yet processed (or was missed). Create a
            // tombstone projection so the ban is not silently lost; the full
            // profile will be populated when UserRegistered arrives.
            var tombstone = UserProjection.Create(userId, string.Empty, string.Empty, string.Empty);
            tombstone.IsActive  = false;
            tombstone.UpdatedAt = DateTime.UtcNow;
            await _dbContext.UserProjections.AddAsync(tombstone, context.CancellationToken);
        }

        _dbContext.ProcessedEvents.Add(new ProcessedEvent(message.Id, nameof(UserBannedEvent), DateTime.UtcNow));

        try
        {
            await _dbContext.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { }
    }
}
