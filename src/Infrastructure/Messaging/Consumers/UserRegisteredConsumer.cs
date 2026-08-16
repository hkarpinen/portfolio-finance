using Finance.Domain.Aggregates;
using Finance.Infrastructure.Persistence.Projections;
using Finance.Domain.ValueObjects;
using Domain.Events;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Messaging.Consumers;

internal sealed class UserRegisteredConsumer : IConsumer<UserRegistered>
{
    private readonly FinanceDbContext _dbContext;

    public UserRegisteredConsumer(FinanceDbContext dbContext) => _dbContext = dbContext;

    public async Task Consume(ConsumeContext<UserRegistered> context)
    {
        var message = context.Message;

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

        await _dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
