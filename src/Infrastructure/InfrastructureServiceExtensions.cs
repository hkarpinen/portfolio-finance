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
        // Backs the global group-membership filter — without it every group-scoped route is
        // authenticated but not authorised.
        services.AddScoped<IGroupQuery, GroupQuery>();
        services.AddScoped<IFinancialConnectionQuery, FinancialConnectionQuery>();

        services.AddHostedService<OutboxPublisher>();

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
