using EventSourcing.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventSourcing.Optimizations.Tests;

public sealed class InjectionSetupTests
{
    [Theory]
    [InlineData(typeof(StateMachineHandler), typeof(StateMachineHandlerWithStateStore))]
    [InlineData(typeof(IStateInfoStore), typeof(StateInfoStoreWithCache))]
    public void RegisterEventSourcingOptmizations_AfterCore_SelectsOptimizedImplementation(
        Type serviceType,
        Type implementationType
    )
    {
        var services = new ServiceCollection();
        services.RegisterEventSourcingCore();
        services.RegisterEventSourcingOptmizations(new ConfigurationBuilder().Build());

        var selected = services.Last(descriptor => descriptor.ServiceType == serviceType);

        Assert.Equal(implementationType, selected.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, selected.Lifetime);
    }
}
