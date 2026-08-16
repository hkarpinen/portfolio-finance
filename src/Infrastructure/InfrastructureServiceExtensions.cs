using Finance.Application.Ports;
using Finance.Application.Queries;
using Finance.Application.Repositories;
using Finance.Domain.Engines;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Consumers;
using Infrastructure.Persistence;
using Infrastructure.Plaid;
using Infrastructure.Queries;
using Infrastructure.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure;

public static class InfrastructureServiceExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<DomainEventPublishingInterceptor>();
        services.AddDbContext<FinanceDbContext>((sp, options) =>
            options.UseNpgsql(
                    configuration.GetConnectionString("Finance"),
                    npgsql => npgsql.MigrationsAssembly("Infrastructure"))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(sp.GetRequiredService<DomainEventPublishingInterceptor>()));

        var rabbitConfig = configuration.GetSection("RabbitMq");
        services.AddMassTransit(x =>
        {
            x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("finance", false));

            // Replaces the hand-rolled outbox table, its polling BackgroundService, and the
            // ProcessedEvents dedup every consumer used to repeat. UseBusOutbox routes a Publish
            // made during SaveChanges into the outbox rather than the broker, so the event commits
            // with the aggregate; the delivery service sends it. The inbox does consumer-side
            // dedup, which is what a redelivered message used to hit a unique-violation catch for.
            x.AddEntityFrameworkOutbox<FinanceDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });

            // Turns the inbox ON for every receive endpoint. AddEntityFrameworkOutbox on its own
            // creates the tables and the send side only — without this the consumers have no dedup
            // at all, which is precisely what the per-consumer ProcessedEvents check used to do.
            // A redelivered message would re-run the handler.
            x.AddConfigureEndpointsCallback((context, _, cfg) =>
                cfg.UseEntityFrameworkOutbox<FinanceDbContext>(context));

            x.AddConsumer<UserRegisteredConsumer>();
            x.AddConsumer<UserProfileUpdatedConsumer>();
            x.AddConsumer<UserBannedConsumer>();
            x.AddConsumer<DemoUserCreatedConsumer>();
            x.AddConsumer<DemoHouseholdSeededConsumer>();
            x.AddConsumer<DemoUserExpiredConsumer>();
            x.AddConsumer<GroupShareAssignedConsumer>();
            x.AddConsumer<HouseholdMembershipConsumer>();
            x.AddConsumer<HouseholdDeletedConsumer>();
            x.AddConsumer<LedgerJournalConsumer, LedgerJournalConsumerDefinition>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitConfig["Host"] ?? "localhost", h =>
                {
                    var username = rabbitConfig["Username"];
                    var password = rabbitConfig["Password"];
                    if (!string.IsNullOrWhiteSpace(username)) h.Username(username);
                    if (!string.IsNullOrWhiteSpace(password)) h.Password(password);
                });

                // Take the converters FROM the outbox options rather than re-listing them, so what
                // MassTransit puts on the wire cannot drift from what the outbox writes on disk.
                // Without them MT emits `{"value":"..."}` envelopes for every value object, which
                // breaks every consumer — they all declare flat Guid fields.
                cfg.ConfigureJsonSerializerOptions(opts =>
                {
                    opts.PropertyNamingPolicy = OutboxExtensions.JsonOptions.PropertyNamingPolicy;
                    opts.DefaultIgnoreCondition = OutboxExtensions.JsonOptions.DefaultIgnoreCondition;
                    foreach (var converter in OutboxExtensions.JsonOptions.Converters)
                        opts.Converters.Add(converter);
                    return opts;
                });

                // Stated rather than defaulted, because the failure mode is silent: a message that
                // gives up leaves the aggregate saved and whatever it should have driven — a ledger
                // entry, a projection row — missing, with nothing reading as broken.
                //
                // In-process retries only. Delayed redelivery would ride a longer outage but needs a
                // message scheduler (the RabbitMQ delayed-exchange plugin) this deployment does not
                // declare, and configuring it without the plugin fails the endpoint at startup.
                //
                // Safe for every consumer here: each is idempotent on a redelivery, and the ledger
                // one is convergent — it re-reads the aggregate rather than replaying an instruction.
                cfg.UseMessageRetry(r => r.Intervals(
                    TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)));

                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddScoped<IIncomeSourceRepository, IncomeSourceRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IShareRepository, ShareRepository>();
        services.AddScoped<IMemberTransferRepository, MemberTransferRepository>();
        services.AddScoped<IReceiptRepository, ReceiptRepository>();
        services.AddScoped<IBankSynchroniser, BankSynchroniser>();
        services.AddScoped<IBankConnections, FinancialConnectionService>();
        services.AddScoped<IFinancialConnectionRepository, FinancialConnectionRepository>();
        services.AddScoped<IFinancialConnectionQuery, FinancialConnectionQuery>();
        services.AddScoped<ILedgerRepository, LedgerRepository>();

        services.AddScoped<IIncomeQuery, IncomeQuery>();
        services.AddScoped<IExpenseQuery, ExpenseQuery>();
        services.AddScoped<ILedgerQuery, LedgerQuery>();
        // Backs the global group-membership filter — without it every group-scoped route is
        // authenticated but not authorised.
        services.AddScoped<IGroupQuery, GroupQuery>();
        services.AddScoped<IRecurringExpenseRepository, RecurringExpenseRepository>();
        services.AddScoped<IFinancialConnectionQuery, FinancialConnectionQuery>();


        services.Configure<PlaidOptions>(configuration.GetSection("Plaid"));
        services.AddDataProtection();
        services.AddScoped<IConnectionTokenProtector, AccessTokenProtector>();
        services.AddScoped<IFinancialConnectionRepository, FinancialConnectionRepository>();

        // Timeout is sized for Plaid's worst-case sync page (~10s) plus headroom for network jitter on
        // the very first sync.
        services.AddHttpClient<IBankDataProvider, PlaidApiClient>(http =>
        {
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<IBankWebhookVerifier, PlaidWebhookVerifier>(http =>
        {
            http.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }

    public static async Task ApplyMigrationsAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        await db.Database.MigrateAsync();
    }
}
