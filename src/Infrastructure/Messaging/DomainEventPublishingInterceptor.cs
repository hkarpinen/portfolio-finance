using Finance.Domain;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Messaging;

/// <summary>
/// Publishes what the aggregates raised, in the transaction that saves them.
///
/// An interceptor rather than a <c>SaveChangesAsync</c> override: saving is what a DbContext does,
/// and a subclass that quietly does something else as well is a trap for whoever calls it. This is
/// registered in DI, is visible in the registration, and can be left out of a context that should
/// not publish.
///
/// MassTransit's bus outbox turns each <c>Publish</c> into a row in ITS outbox table on this same
/// context, so the events commit with the aggregate and its delivery service does the sending. That
/// is why nothing here talks to the broker and why there is no polling loop to own.
/// </summary>
internal sealed class DomainEventPublishingInterceptor : SaveChangesInterceptor
{
    // Resolved when saving, not when constructed. Under UseBusOutbox the publish endpoint reaches
    // back for this same DbContext, so taking IPublishEndpoint as a constructor argument makes
    // building the context require building the endpoint require building the context. That cycle
    // does not fail loudly — `dotnet ef` simply hangs forever.
    private readonly IServiceProvider _services;

    public DomainEventPublishingInterceptor(IServiceProvider services) => _services = services;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            await PublishAsync(eventData.Context, cancellationToken);

        return result;
    }

    private async Task PublishAsync(DbContext context, CancellationToken cancellationToken)
    {
        var raised = context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Select(e => e.Entity)
            .Where(a => a.GetDomainEvents().Count > 0)
            .ToList();

        foreach (var aggregate in raised)
        {
            foreach (var domainEvent in aggregate.GetDomainEvents())
            {
                // Ledger housekeeping raises events on every posting that carry nothing a consumer
                // needs; publishing them would echo every posting back onto the bus for the ledger
                // consumer to react to. The allow-list is a domain decision, not plumbing, which is
                // why it survives the move off the hand-rolled outbox.
                if (!PublishedEvents.Includes(domainEvent.GetType())) continue;

                var publishEndpoint = _services.GetRequiredService<IPublishEndpoint>();
                await publishEndpoint.Publish(domainEvent, domainEvent.GetType(), cancellationToken);
            }

            aggregate.ClearDomainEvents();
        }
    }
}
