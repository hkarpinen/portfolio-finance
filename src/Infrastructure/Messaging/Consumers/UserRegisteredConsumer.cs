using Bills.Domain.Aggregates;
using Bills.Domain.ReadModels;
using Bills.Domain.ValueObjects;
using Infrastructure.Messaging.Events;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Messaging.Consumers;

internal sealed class UserRegisteredConsumer : IConsumer<UserRegisteredEvent>
{
    private readonly BillsDbContext _dbContext;

    public UserRegisteredConsumer(BillsDbContext dbContext) => _dbContext = dbContext;

    public async Task Consume(ConsumeContext<UserRegisteredEvent> context)
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
            var projection = UserProjection.Create(userId, message.Email, firstName, lastName);
            await _dbContext.UserProjections.AddAsync(projection, context.CancellationToken);
        }
        else
        {
            existing.Email     = message.Email;
            existing.FirstName = firstName;
            existing.LastName  = lastName;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        _dbContext.ProcessedEvents.Add(new ProcessedEvent(message.Id, nameof(UserRegisteredEvent), DateTime.UtcNow));

        try
        {
            await _dbContext.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { }
    }
}
