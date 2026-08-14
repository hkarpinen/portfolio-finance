using Finance.Application.Managers;
using Finance.Domain.ValueObjects;
using Household.Domain.Events;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Messaging.Consumers;

// The event's UserId is authoritative and there is no caller to override it. That is exactly why a
// role-gated "add a split for another member" has to arrive this way rather than through the
// public /splits endpoint, which force-attributes the share to the caller.
internal sealed class GroupShareAssignedConsumer : IConsumer<GroupShareAssigned>
{
    private readonly FinanceDbContext _dbContext;
    private readonly IExpenseManager _expenses;

    public GroupShareAssignedConsumer(FinanceDbContext dbContext, IExpenseManager expenses)
    {
        _dbContext = dbContext;
        _expenses = expenses;
    }

    public async Task Consume(ConsumeContext<GroupShareAssigned> context)
    {
        var message = context.Message;
        if (await _dbContext.ProcessedEvents.AnyAsync(e => e.EventId == message.Id, context.CancellationToken))
            return;

        // Journaling rides the ShareCreated/ShareUpdated event the upsert raises — the same
        // single path every other share write takes — so nothing is posted to the ledger here.
        await _expenses.AssignShareAsync(
            message.GroupId, message.ExpenseId, message.UserId, message.Amount, message.Currency,
            context.CancellationToken);

        _dbContext.ProcessedEvents.Add(new ProcessedEvent(message.Id, nameof(GroupShareAssigned), DateTime.UtcNow));
        try
        {
            await _dbContext.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { }
    }
}
