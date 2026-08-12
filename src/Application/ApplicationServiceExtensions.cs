using Finance.Application.Managers;
using Finance.Application.Managers.Demo;
using Finance.Application.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Application;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IIncomeManager, IncomeManager>();
        services.AddScoped<IChargeManager, ChargeManager>();
        services.AddScoped<IChargeScheduleManager, ChargeScheduleManager>();
        services.AddScoped<IFinancialConnectionManager, FinancialConnectionManager>();
        services.AddScoped<IBankSyncManager, BankSyncManager>();
        services.AddScoped<IContributionCalculator, ContributionCalculator>();
        services.AddScoped<IDemoSeedManager, DemoSeedManager>();
        services.AddScoped<IBookkeepingManager, BookkeepingManager>();

        return services;
    }
}


