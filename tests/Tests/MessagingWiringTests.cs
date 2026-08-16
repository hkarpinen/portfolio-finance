using Infrastructure;
using Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tests;

/// <summary>
/// That the container can actually build what the app asks it for.
///
/// MassTransit's bus outbox makes IPublishEndpoint reach back for the DbContext, and the
/// interceptor that drains domain events needs a publish endpoint. Taking IPublishEndpoint as a
/// constructor argument closes that loop, and the loop does not throw — resolution simply never
/// returns. `dotnet ef` hung for seven minutes on it with no output at all.
///
/// Nothing else here catches it: it compiles, and every other test builds its objects by hand
/// rather than through the container.
/// </summary>
public class MessagingWiringTests
{
    private static ServiceProvider BuildContainer()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Finance"] = "Host=wiring-only;Database=none;Username=none;Password=none",
                ["RabbitMq:Host"] = "wiring-only",
            })
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(configuration)
            .BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public async Task TheDbContextAndThePublishEndpointCanBothBeResolved()
    {
        await using var provider = BuildContainer();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<FinanceDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>());
    }

    /// <summary>The tables MassTransit's outbox and inbox need are part of the model.</summary>
    [Fact]
    public async Task TheOutboxAndInboxEntitiesAreMapped()
    {
        await using var provider = BuildContainer();
        using var scope = provider.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Model;

        foreach (var table in new[] { "inbox_state", "outbox_state", "outbox_message" })
            Assert.Contains(table, model.GetEntityTypes().Select(e => e.GetTableName()));
    }
}
