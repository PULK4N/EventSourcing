using EventSourcing.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Optimizations;

public static class InjectionSetup
{
    public static IServiceCollection RegisterEventSourcingOptmizations(
        this IServiceCollection service,
        IConfiguration configuration
    )
    {
        service.AddScoped<IEventStore, EventStoreWithCache>();

        return service;
    }
}
