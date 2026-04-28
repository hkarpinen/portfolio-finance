using System.Text.Json;
using Bills.Domain.Events;
using Infrastructure.Messaging.Events;
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

    private static readonly Dictionary<string, Type> EventTypeMap = new()
    {
        [nameof(HouseholdCreated)]               = typeof(BillsHouseholdCreatedEvent),
        [nameof(HouseholdOwnershipTransferred)]  = typeof(BillsHouseholdOwnershipTransferredEvent),
        [nameof(HouseholdMemberJoined)]          = typeof(BillsHouseholdMemberJoinedEvent),
        [nameof(HouseholdMemberLeft)]            = typeof(BillsHouseholdMemberLeftEvent),
        [nameof(HouseholdMemberRemoved)]         = typeof(BillsHouseholdMemberRemovedEvent),
        [nameof(HouseholdMemberRoleChanged)]     = typeof(BillsHouseholdMemberRoleChangedEvent),
        [nameof(BillCreated)]                    = typeof(BillsBillCreatedEvent),
        [nameof(BillSplitCreated)]               = typeof(BillsBillSplitCreatedEvent),
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessOutboxAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BillsDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var messages = await dbContext.OutboxMessages
            .FromSqlRaw("""
                SELECT * FROM bills.outbox_messages
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
