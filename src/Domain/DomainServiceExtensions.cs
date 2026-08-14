using Finance.Domain.Engines;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Domain;

public static class DomainServiceExtensions
{
    public static IServiceCollection AddDomain(this IServiceCollection services)
    {
        services.AddScoped<IBankSyncMatchingEngine, BankSyncMatchingEngine>();
        services.AddScoped<IGroupCoverageEngine, GroupCoverageEngine>();
        services.AddScoped<IPayrollDeductionEngine, PayrollDeductionEngine>();
        return services;
    }
}
