using Finance.Application.Managers.Dependencies;
using Finance.Application.Queries;
using Infrastructure.Engines;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Consumers;
using Infrastructure.Persistence;
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

        services.AddScoped<IHouseholdRepository, HouseholdRepository>();
        services.AddScoped<IHouseholdMembershipRepository, HouseholdMembershipRepository>();
        services.AddScoped<IBillRepository, BillRepository>();
        services.AddScoped<IBillSplitRepository, BillSplitRepository>();
        services.AddScoped<IIncomeSourceRepository, IncomeSourceRepository>();
        services.AddScoped<IPersonalBillRepository, PersonalBillRepository>();
        services.AddScoped<IHouseholdCoverageEngine, HouseholdCoverageEngine>();

        services.AddScoped<IBillQuery, BillQuery>();
        services.AddScoped<IHouseholdQuery, HouseholdQuery>();
        services.AddScoped<IHouseholdMembershipQuery, HouseholdMembershipQuery>();
        services.AddScoped<IIncomeQuery, IncomeQuery>();
        services.AddScoped<IDashboardQuery, DashboardQuery>();
        services.AddScoped<IBillSplitQuery, BillSplitQuery>();
        services.AddScoped<IPersonalBillQuery, PersonalBillQuery>();

        services.AddHostedService<OutboxPublisher>();

        return services;
    }
}
