using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Providers;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static EventSourcing.Core.Tests.StateMachineEventHookProviderTests;

namespace EventSourcing.Core.Tests;

public sealed class EventHookRegistrationTests
{
    [Fact]
    public async Task DefaultProviderResolvesHooksAndDependenciesWithinScope()
    {
        var services = new ServiceCollection();
        services.RegisterEventSourcingCore();
        services.Replace(ServiceDescriptor.Singleton(
            Definitions(Definition("hooks", nameof(ScopedHook)))));
        services.AddScoped<HookDependency>();
        services.AddScoped<IEventHook, ScopedHook>();
        using var root = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        ScopedHook firstHook;
        IEventHookProvider firstProvider;
        using (var scope = root.CreateScope())
        {
            firstProvider = scope.ServiceProvider.GetRequiredService<IEventHookProvider>();
            Assert.IsType<StateMachineEventHookProvider>(firstProvider);
            Assert.Same(firstProvider,
                scope.ServiceProvider.GetRequiredService<IEventHookProvider>());
            firstHook = Assert.IsType<ScopedHook>(
                Assert.Single(await firstProvider.GetHooks(Payload("hooks"))));
            Assert.Same(scope.ServiceProvider.GetRequiredService<HookDependency>(),
                firstHook.Dependency);
            Assert.False(firstHook.Dependency.Disposed);
        }
        Assert.True(firstHook.Dependency.Disposed);

        using var nextScope = root.CreateScope();
        var nextProvider = nextScope.ServiceProvider.GetRequiredService<IEventHookProvider>();
        var nextHook = Assert.Single(await nextProvider.GetHooks(Payload("hooks")));
        Assert.NotSame(firstProvider, nextProvider);
        Assert.NotSame(firstHook, nextHook);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CustomProviderOverridesDefaultWithoutLoadingYaml(bool registerBeforeCore)
    {
        var services = new ServiceCollection();
        if (registerBeforeCore)
            services.AddScoped<IEventHookProvider, CustomProvider>();

        services.RegisterEventSourcingCore();

        if (!registerBeforeCore)
            services.Replace(ServiceDescriptor.Scoped<IEventHookProvider, CustomProvider>());

        var descriptor = Assert.Single(services,
            descriptor => descriptor.ServiceType == typeof(IEventHookProvider));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        using var root = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });
        using var scope = root.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IEventHookProvider>();
        Assert.IsType<CustomProvider>(provider);
        Assert.IsType<RecordingHook>(Assert.Single(await provider.GetHooks(Payload("custom"))));
    }

    private sealed class HookDependency : IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    private sealed class ScopedHook(HookDependency dependency) : IEventHook
    {
        public HookDependency Dependency { get; } = dependency;
        public Task Execute(EventPayload payload, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class CustomProvider : IEventHookProvider
    {
        public Task<List<IEventHook>> GetHooks(
            EventPayload payload, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<IEventHook> { new RecordingHook() });
    }
}
