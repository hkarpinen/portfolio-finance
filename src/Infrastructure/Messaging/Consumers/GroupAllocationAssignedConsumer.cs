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
// public /splits endpoint, which force-attributes the allocation to the caller.
internal sealed class GroupAllocationAssignedConsumer : IConsumer<GroupAllocationAssigned>
{
    private readonly FinanceDbContext _dbContext;
    private readonly IChargeManager _charges;

    public GroupAllocationAssignedConsumer(FinanceDbContext dbContext, IChargeManager charges)
    {
        _dbContext = dbContext;
        _charges = charges;
    }

    public async Task Consume(ConsumeContext<GroupAllocationAssigned> context)
    {
        var message = context.Message;
        if (await _dbContext.ProcessedEvents.AnyAsync(e => e.EventId == message.Id, context.CancellationToken))
            return;

        // Journaling rides the AllocationCreated/AllocationUpdated event the upsert raises — the same
        // single path every other allocation write takes — so nothing is posted to the ledger here.
        await _charges.AssignAllocationAsync(
            message.GroupId, message.ChargeId, message.UserId, message.Amount, message.Currency,
            context.CancellationToken);

        _dbContext.ProcessedEvents.Add(new ProcessedEvent(message.Id, nameof(GroupAllocationAssigned), DateTime.UtcNow));
        try
        {
            await _dbContext.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) { }
    }
}
