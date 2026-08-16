using EventSourcing.Persistence;
using EventSourcing.Persistence.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EventSourcing.Core.Tests;

public sealed class OutboxRegistrationTests
{
    [Fact]
    public void PersistenceRegistrationRegistersDurableOutbox()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["UseSqlServer"] = bool.FalseString
                }
            )
            .Build();
        var services = new ServiceCollection();

        services.RegisterEventSourcingPersistence(configuration);

        var descriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IOutbox)
        );
        Assert.Equal(typeof(Outbox), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void ProjectionOutboxCanReplaceDurableOutboxThroughDependencyInjection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["UseSqlServer"] = bool.FalseString
                }
            )
            .Build();
        var services = new ServiceCollection();
        services.RegisterEventSourcingPersistence(configuration);
        services.RegisterEventSourcingCore(typeof(Registration).Assembly);

        services.Replace(
            ServiceDescriptor.Scoped<IOutbox, ProjectionOutbox>()
        );

        var descriptor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(IOutbox)
        );
        Assert.Equal(
            typeof(ProjectionOutbox),
            descriptor.ImplementationType
        );
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void CoreRegistrationRegistersScopedProcessorAndHostedOutboxWorker()
    {
        var services = new ServiceCollection();

        services.RegisterEventSourcingCore(typeof(Registration).Assembly);

        var processor = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(OutboxMessageProcessor)
        );
        Assert.Equal(ServiceLifetime.Scoped, processor.Lifetime);

        var worker = Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(OutboxBackgroundService)
        );
        Assert.Equal(ServiceLifetime.Singleton, worker.Lifetime);
    }
}
