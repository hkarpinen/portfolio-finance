using Finance.Application.Managers;
using Finance.Domain.ValueObjects;
using Household.Domain.Events;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Infrastructure.Messaging.Consumers;

// The event's UserId is authoritative and there is no caller to override it. That is exactly why a
// role-gated "add a share for another member" has to arrive this way rather than through the
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

        // Journaling rides the ShareCreated/ShareUpdated event the upsert raises — the same
        // single path every other share write takes — so nothing is posted to the ledger here.
        await _expenses.AssignShareAsync(
            groupId: message.GroupId,
            expenseId: message.ExpenseId,
            userId: message.UserId,
            amount: message.Amount,
            currency: message.Currency,
            cancellationToken: context.CancellationToken);
        await _dbContext.SaveChangesAsync(context.CancellationToken);
    }
}
