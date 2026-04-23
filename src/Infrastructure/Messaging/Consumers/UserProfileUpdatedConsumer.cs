using Bills.Domain.ValueObjects;
using Infrastructure.Messaging.Events;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Messaging.Consumers;

internal sealed class UserProfileUpdatedConsumer : IConsumer<UserProfileUpdatedEvent>
{
    private readonly BillsDbContext _dbContext;

    public UserProfileUpdatedConsumer(BillsDbContext dbContext) => _dbContext = dbContext;

    public async Task Consume(ConsumeContext<UserProfileUpdatedEvent> context)
    {
        var message = context.Message;
        if (await _dbContext.ProcessedEvents.AnyAsync(e => e.EventId == message.Id, context.CancellationToken))
            return;

        var userId = new UserId(message.UserId);
        var existing = await _dbContext.UserProjections
            .FirstOrDefaultAsync(u => u.UserId == userId, context.CancellationToken);

        if (existing is not null)
        {
            var nameParts = (message.DisplayName ?? "").Split(' ', 2);
            existing.FirstName = nameParts.Length > 0 ? nameParts[0] : existing.FirstName;
            existing.LastName  = nameParts.Length > 1 ? nameParts[1] : existing.LastName;
            existing.AvatarUrl = message.AvatarUrl ?? existing.AvatarUrl;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        _dbContext.ProcessedEvents.Add(new ProcessedEvent(message.Id, nameof(UserProfileUpdatedEvent), DateTime.UtcNow));

        try
        {
            await _dbContext.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { }
    }
}
