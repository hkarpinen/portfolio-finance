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
        services.AddDbContext<FinanceDbContext>(options =>
            options.UseNpgsql(
                    configuration.GetConnectionString("Finance"),
                    npgsql => npgsql.MigrationsAssembly("Infrastructure"))
                .UseSnakeCaseNamingConvention());

        var rabbitConfig = configuration.GetSection("RabbitMq");
        services.AddMassTransit(x =>
        {
            x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter("finance", false));

            x.AddConsumer<UserRegisteredConsumer>();
            x.AddConsumer<UserProfileUpdatedConsumer>();
            x.AddConsumer<UserBannedConsumer>();
            x.AddConsumer<DemoUserCreatedConsumer>();
            x.AddConsumer<DemoHouseholdSeededConsumer>();
            x.AddConsumer<DemoUserExpiredConsumer>();
            x.AddConsumer<GroupAllocationAssignedConsumer>();
            x.AddConsumer<HouseholdMembershipConsumer>();
            x.AddConsumer<HouseholdDeletedConsumer>();
            // Finance consumes its OWN charge/allocation/settlement/vendor events to keep the
            // double-entry ledger in step — the outbox makes posting reliable, the consumer
            // definition serializes it (one message at a time).
            x.AddConsumer<LedgerPostingConsumer, LedgerPostingConsumerDefinition>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitConfig["Host"] ?? "localhost", h =>
                {
                    var username = rabbitConfig["Username"];
                    var password = rabbitConfig["Password"];
                    if (!string.IsNullOrWhiteSpace(username)) h.Username(username);
                    if (!string.IsNullOrWhiteSpace(password)) h.Password(password);
                });

                // Reuse the outbox-side custom converters so what MassTransit puts on
                // the wire matches what `OutboxExtensions.JsonOptions` writes on disk:
                // flat GUIDs for value-object IDs, camelCase enum-name strings, and a
                // nested-but-flat `RecurrenceSchedule` object. Without this MT defaults
                // produce `{"value":"..."}` envelopes for every value-object, which
                // breaks every cross-service consumer (they declare flat Guid fields).
                cfg.ConfigureJsonSerializerOptions(opts =>
                {
                    opts.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                    opts.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                    opts.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
                    opts.Converters.Add(new ChargeIdConverter());
                    opts.Converters.Add(new AllocationIdConverter());
                    opts.Converters.Add(new GroupIdConverter());
                    opts.Converters.Add(new BillsUserIdConverter());
                    opts.Converters.Add(new MoneyConverter());
                    opts.Converters.Add(new RecurrenceScheduleConverter());
                    return opts;
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddScoped<IIncomeSourceRepository, IncomeSourceRepository>();
        services.AddScoped<IChargeRepository, ChargeRepository>();
        services.AddScoped<IChargePaymentRepository, ChargePaymentRepository>();
        services.AddScoped<IAllocationRepository, AllocationRepository>();
        services.AddScoped<ILedgerRepository, LedgerRepository>();

        services.AddScoped<IIncomeQuery, IncomeQuery>();
        services.AddScoped<IChargeQuery, ChargeQuery>();
        services.AddScoped<ILedgerQuery, LedgerQuery>();
        services.AddScoped<IFinancialConnectionQuery, FinancialConnectionQuery>();

        services.AddHostedService<OutboxPublisher>();

        // ── Plaid integration ──────────────────────────────────────────────
        services.Configure<PlaidOptions>(configuration.GetSection("Plaid"));
        services.AddDataProtection();
        services.AddScoped<IConnectionTokenProtector, AccessTokenProtector>();
        services.AddScoped<IFinancialConnectionRepository, FinancialConnectionRepository>();

        // Typed HttpClient: timeout tuned for Plaid's worst-case sync page (~10s),
        // with a bit of headroom for network jitter on the very first sync.
        services.AddHttpClient<IBankDataProvider, PlaidApiClient>(http =>
        {
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient<IPlaidWebhookVerifier, PlaidWebhookVerifier>(http =>
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
