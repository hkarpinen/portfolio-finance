using Finance.Application.Managers;
using Finance.Application.Managers.Demo;
using Finance.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Application;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IIncomeManager, IncomeManager>();
        services.AddScoped<IExpenseManager, ExpenseManager>();
        services.AddScoped<IFinancialConnectionManager, FinancialConnectionManager>();
        services.AddScoped<IBankSyncService, BankSyncService>();
        services.AddScoped<IDemoSeedManager, DemoSeedManager>();

        return services;
    }
}


