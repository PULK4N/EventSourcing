using EventSourcing.Core.Interfaces;
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
        service.AddScoped<IEventStore, EventStoreWithOutbox>();
        service.AddScoped<IOutbox, Outbox>();
        service.AddScoped<BaseSqlEventStore>();

        var connectionString = configuration.GetConnectionString("ApplicationDatabase");
        var contextOptions = new DbContextOptionsBuilder<EventSourcingDbContext>();
        service.AddDbContext<EventSourcingDbContext>(
            options => options.UseSqlServer(connectionString)
        );

        return service;
    }
}
