using Finance.Application.Managers;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Application;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IHouseholdWorkflowManager, HouseholdWorkflowManager>();
        services.AddScoped<IHouseholdMembershipManager, HouseholdMembershipManager>();
        services.AddScoped<IBillWorkflowManager, BillWorkflowManager>();
        services.AddScoped<IIncomeManager, IncomeManager>();
        services.AddScoped<IPersonalBillManager, PersonalBillManager>();

        return services;
    }
}

