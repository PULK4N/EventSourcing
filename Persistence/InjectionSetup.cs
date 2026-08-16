using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Persistence;

public static class InjectionSetup
{
    public static IServiceCollection RegisterEventSourcingPersistence(
        this IServiceCollection service,
        IConfiguration configuration
    )
    {
        service.AddScoped<IEventStore, EventStore>();
        service.AddScoped<IOutbox, Outbox>();
        service.AddScoped<IEventStoreWithOutbox, EventStoreWithOutbox>();
        service.AddScoped<BaseSqlEventStore>();

        var connectionString = configuration.GetConnectionString("ApplicationDatabase");
        var contextOptions = new DbContextOptionsBuilder<EventSourcingDbContext>();

        var useSqlServerConfig = configuration["UseSqlServer"]?.ToString() ?? "true";

        bool.TryParse(useSqlServerConfig, out var useSqlServer);

        if (useSqlServer)
        {
            service.AddDbContext<EventSourcingDbContext>(options =>
            {
                options.UseSqlServer(connectionString);
            });
            DatabaseFriendlyGuidGenerator.SetDefaultGuidGenerationDatabase(
                UUIDNext.Database.SqlServer
            );
        }

        return service;
    }
}
