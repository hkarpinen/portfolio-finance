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
            x.SetKebabCaseEndpointNameFormatter();

            x.AddConsumer<UserRegisteredConsumer>();
            x.AddConsumer<UserProfileUpdatedConsumer>();
            x.AddConsumer<UserBannedConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitConfig["Host"] ?? "localhost", h =>
                {
                    var username = rabbitConfig["Username"];
                    var password = rabbitConfig["Password"];
                    if (!string.IsNullOrWhiteSpace(username)) h.Username(username);
                    if (!string.IsNullOrWhiteSpace(password)) h.Password(password);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        services.AddScoped<IIncomeSourceRepository, IncomeSourceRepository>();
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IExpensePaymentRepository, ExpensePaymentRepository>();
        services.AddScoped<IExpenseSplitRepository, ExpenseSplitRepository>();
        services.AddScoped<IExpenseSplitPaymentRepository, ExpenseSplitPaymentRepository>();

        services.AddScoped<IIncomeQuery, IncomeQuery>();
        services.AddScoped<IExpenseQuery, ExpenseQuery>();
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

        return services;
    }
}
