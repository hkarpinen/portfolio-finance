using System.Text.Json;
using Finance.Domain.Events;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Messaging;

internal sealed class OutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisher> _logger;

    // EventType column stores domainEvent.GetType().Name (see OutboxExtensions.AddToOutbox),
    // so map by simple class name. Publishing the domain-event records directly relies on
    // MassTransit's namespace+name routing — consumers must declare matching types in the
    // Finance.Domain.Events namespace. Household consumes ChargeCreated and
    // SettlementRecorded for its W2-H4 activity feed; finance's own LedgerPostingConsumer
    // consumes the charge/allocation/settlement/vendor events to keep the ledger in step;
    // the rest are listed so future consumers do not silently dead-letter.
    private static readonly Dictionary<string, Type> EventTypeMap = new()
    {
        [nameof(ChargeCreated)] = typeof(ChargeCreated),
        [nameof(ChargeUpdated)] = typeof(ChargeUpdated),
        [nameof(ChargeDeactivated)] = typeof(ChargeDeactivated),
        [nameof(ChargeActivated)] = typeof(ChargeActivated),
        [nameof(ChargePaid)] = typeof(ChargePaid),
        [nameof(ChargeUnpaid)] = typeof(ChargeUnpaid),
        [nameof(VendorPaid)] = typeof(VendorPaid),
        [nameof(VendorPaymentReversed)] = typeof(VendorPaymentReversed),
        [nameof(AllocationCreated)] = typeof(AllocationCreated),
        [nameof(AllocationUpdated)] = typeof(AllocationUpdated),
        [nameof(AllocationRemoved)] = typeof(AllocationRemoved),
        [nameof(SettlementRecorded)] = typeof(SettlementRecorded),
        [nameof(SettlementReversed)] = typeof(SettlementReversed),
    };

    // Read with the same converters AddToOutbox writes with — otherwise round-trip
    // fails on value objects whose constructors are private (Money, RecurrenceSchedule)
    // or whose JSON shape is flattened (ChargeId, AllocationId, GroupId, UserId).
    private static readonly JsonSerializerOptions JsonOptions = OutboxExtensions.JsonOptions;

    public OutboxPublisher(IServiceScopeFactory scopeFactory, ILogger<OutboxPublisher> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            // Short cadence: ledger postings ride this loop (state change → outbox → consumer),
            // so the poll interval is the ceiling on how stale a just-written balance can read.
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    private async Task ProcessOutboxAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var messages = await dbContext.OutboxMessages
            .FromSqlRaw("""
                SELECT * FROM finance.outbox_messages
                WHERE published = false AND dead_lettered = false
                ORDER BY created_at
                LIMIT 50
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            if (!EventTypeMap.TryGetValue(message.EventType, out var messageType))
            {
                _logger.LogWarning("Unknown event type {EventType} on message {Id} — dead-lettering", message.EventType, message.Id);
                message.DeadLettered = true;
                message.LastError = $"Unknown event type: {message.EventType}";
                message.LastAttemptAt = DateTime.UtcNow;
                continue;
            }

            try
            {
                var @event = JsonSerializer.Deserialize(message.Payload, messageType, JsonOptions);
                if (@event is null)
                {
                    message.DeadLettered = true;
                    message.LastError = "Payload deserialized to null";
                    message.LastAttemptAt = DateTime.UtcNow;
                    continue;
                }

                await publishEndpoint.Publish(@event, messageType, cancellationToken);

                message.Published = true;
                message.PublishedAt = DateTime.UtcNow;
                message.LastAttemptAt = DateTime.UtcNow;

                _logger.LogInformation("Published outbox message {Id} of type {EventType}",
                    message.Id, message.EventType);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.LastError = ex.Message.Length > 2048 ? ex.Message[..2048] : ex.Message;
                message.LastAttemptAt = DateTime.UtcNow;

                if (message.RetryCount >= OutboxMessage.MaxRetryCount)
                {
                    message.DeadLettered = true;
                    _logger.LogError(ex, "Outbox message {Id} exceeded {Max} retries — dead-lettered",
                        message.Id, OutboxMessage.MaxRetryCount);
                }
                else
                {
                    _logger.LogWarning(ex, "Failed to publish outbox message {Id} (attempt {Attempt}/{Max})",
                        message.Id, message.RetryCount, OutboxMessage.MaxRetryCount);
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
