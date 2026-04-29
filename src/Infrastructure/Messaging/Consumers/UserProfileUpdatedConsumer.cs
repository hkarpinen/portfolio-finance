using Finance.Domain.ReadModels;
using Finance.Domain.ValueObjects;
using Infrastructure.Messaging.Events;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Messaging.Consumers;

internal sealed class UserProfileUpdatedConsumer : IConsumer<UserProfileUpdatedEvent>
{
    private readonly FinanceDbContext _dbContext;

    public UserProfileUpdatedConsumer(FinanceDbContext dbContext) => _dbContext = dbContext;

    public async Task Consume(ConsumeContext<UserProfileUpdatedEvent> context)
    {
        var message = context.Message;
        if (await _dbContext.ProcessedEvents.AnyAsync(e => e.EventId == message.Id, context.CancellationToken))
            return;

        var userId = new UserId(message.UserId);
        var existing = await _dbContext.UserProjections
            .FirstOrDefaultAsync(u => u.UserId == userId, context.CancellationToken);

        var nameParts = (message.DisplayName ?? "").Split(' ', 2);
        var firstName = nameParts.Length > 0 ? nameParts[0] : string.Empty;
        var lastName  = nameParts.Length > 1 ? nameParts[1] : string.Empty;

        if (existing is null)
        {
            // UserRegistered was missed — recover the projection from this event.
            var projection = UserProjection.Create(userId, string.Empty, firstName, lastName, message.AvatarUrl);
            await _dbContext.UserProjections.AddAsync(projection, context.CancellationToken);
        }
        else
        {
            existing.FirstName = firstName.Length > 0 ? firstName : existing.FirstName;
            existing.LastName  = lastName.Length  > 0 ? lastName  : existing.LastName;
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
