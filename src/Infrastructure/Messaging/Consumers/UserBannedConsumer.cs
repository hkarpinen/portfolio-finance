using Finance.Infrastructure.Persistence.Projections;
using Finance.Domain.ValueObjects;
using Domain.Events;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Messaging.Consumers;

internal sealed class UserBannedConsumer : IConsumer<UserBanned>
{
    private readonly FinanceDbContext _dbContext;

    public UserBannedConsumer(FinanceDbContext dbContext) => _dbContext = dbContext;

    public async Task Consume(ConsumeContext<UserBanned> context)
    {
        var message = context.Message;

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
            // UserRegistered has not been processed yet (or was missed). Write a tombstone so the ban is not
            // silently lost; the full profile fills in when UserRegistered arrives.
            var tombstone = UserProjection.Create(userId, string.Empty, string.Empty, string.Empty);
            tombstone.IsActive  = false;
            tombstone.UpdatedAt = DateTime.UtcNow;
            await _dbContext.UserProjections.AddAsync(tombstone, context.CancellationToken);
        }

        await _dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
