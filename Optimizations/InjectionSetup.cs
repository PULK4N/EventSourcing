using EventSourcing.Persistence;
using EventSourcing.Persistence.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventSourcing.Optimizations;

public static class InjectionSetup
{
    public static IServiceCollection RegisterEventSourcingOptmizations(
        this IServiceCollection service,
        IConfiguration configuration
    )
    {
        service.AddMemoryCache();
        service.TryAddScoped<BaseSqlEventStore>();
        service.AddScoped<IEventStore, EventStoreWithCache>();

        return service;
    }
}
