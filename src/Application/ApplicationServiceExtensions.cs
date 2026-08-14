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
        services.AddScoped<IExpenseManager, ExpenseManager>();
        services.AddScoped<IRecurringExpenseManager, RecurringExpenseManager>();
        services.AddScoped<IContributionCalculator, ContributionCalculator>();
        services.AddScoped<IDemoSeedManager, DemoSeedManager>();
        services.AddScoped<IBookkeepingManager, BookkeepingManager>();

        return services;
    }
}


