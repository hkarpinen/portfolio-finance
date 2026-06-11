using Finance.Application.Managers;
using Finance.Domain.ValueObjects;
using Household.Domain.Events;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Messaging.Consumers;

/// <summary>
/// Applies a household-authorized allocation to finance. Household owns the role check (a member
/// may assign their own share; Owner/Admin may assign another member's), then emits
/// <see cref="GroupAllocationAssigned"/>; finance just applies it. The event's UserId is
/// authoritative — there is no caller to override here, which is exactly why the role-gated
/// "add a split for another member" must arrive this way rather than via the public /splits
/// endpoint (which force-attributes to the caller). Fully async, no service-to-service call.
/// </summary>
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

        // The upsert commits an AllocationCreated/AllocationUpdated event with it; the
        // LedgerPostingConsumer journals the share (Dr Member / Cr Expense) from that —
        // the same single path every other allocation write takes.
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
