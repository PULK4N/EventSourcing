using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Models;
using EventSourcing.Core.Providers;
using EventSourcing.Shared.Exceptions;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;
using Moq;

namespace EventSourcing.Core.Tests;

public sealed class StateMachineEventHookProviderTests
{
    [Theory]
    [InlineData("first", nameof(YamlUserCreated), true)]
    [InlineData("second", nameof(YamlUserCreated), false)]
    [InlineData("first", "UnconfiguredEvent", false)]
    public async Task SelectsByMachineAndEventWithoutExecutingHooks(
        string machineId, string eventName, bool expectedHook)
    {
        var hook = new RecordingHook();
        var first = Definition("first", nameof(RecordingHook));
        var second = Definition("second");
        var provider = new StateMachineEventHookProvider(Definitions(first, second), [hook]);
        var payload = Payload(machineId);
        payload.EventExecutionInfo = new EventExecutionInfo
        {
            StateMachineId = machineId,
            EventName = eventName
        };

        var selected = await provider.GetHooks(payload);

        if (expectedHook)
            Assert.Same(hook, Assert.Single(selected));
        else
            Assert.Empty(selected);
        Assert.Equal(0, hook.Executions);
        selected.Clear();
        Assert.Equal(expectedHook ? 1 : 0, (await provider.GetHooks(payload)).Count);
    }

    [Theory]
    [InlineData("MissingHook")]
    [InlineData("recordinghook")]
    [InlineData("")]
    public void RejectsUnknownOrInvalidConfiguredNames(string hookName)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new StateMachineEventHookProvider(
                Definitions(Definition("first", hookName)), [new RecordingHook()]));

        Assert.Contains(hookName, exception.Message);
        Assert.Contains("first", exception.Message);
        Assert.Contains(nameof(YamlUserCreated), exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectsDuplicateClassNames(bool differentTypes)
    {
        IEventHook second = differentTypes
            ? new OtherHooks.RecordingHook()
            : new RecordingHook();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new StateMachineEventHookProvider(
                Definitions(Definition("first")), [new RecordingHook(), second]));

        Assert.Contains(nameof(RecordingHook), exception.Message);
    }

    [Fact]
    public async Task PropagatesUnknownMachineAndCancellation()
    {
        var definitions = new Mock<IStateMachineDefinitionProvider>();
        definitions.Setup(x => x.GetAll()).Returns(new List<StateMachineDefinition>());
        definitions.Setup(x => x.Get("unknown"))
            .Throws(new StateMachineNotRegisteredException("unknown"));
        var provider = new StateMachineEventHookProvider(definitions.Object, []);

        await Assert.ThrowsAsync<StateMachineNotRegisteredException>(() =>
            provider.GetHooks(Payload("unknown")));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.GetHooks(Payload("unknown"), new CancellationToken(true)));
        definitions.Verify(x => x.Get("unknown"), Times.Once);
    }

    internal static StateMachineDefinition Definition(string id, params string[] names) => new()
    {
        Id = id,
        Events =
        {
            [nameof(YamlUserCreated)] = new StateMachineEventDefinition
            {
                Hooks = new HashSet<string>(names, StringComparer.Ordinal)
            }
        }
    };

    internal static IStateMachineDefinitionProvider Definitions(
        params StateMachineDefinition[] definitions)
    {
        var mock = new Mock<IStateMachineDefinitionProvider>();
        mock.Setup(x => x.GetAll()).Returns(definitions.ToList());
        foreach (var definition in definitions)
            mock.Setup(x => x.Get(definition.Id)).Returns(definition);
        return mock.Object;
    }

    internal static EventPayload Payload(string machineId) => EventPayload.Create(
        EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
        AggregateId.FromDatabaseGuid(Guid.NewGuid()),
        machineId,
        new YamlUserCreated());

    internal sealed class RecordingHook : IEventHook
    {
        public int Executions { get; private set; }

        public Task Execute(EventPayload payload, CancellationToken cancellationToken = default)
        {
            Executions++;
            return Task.CompletedTask;
        }
    }

    private static class OtherHooks
    {
        internal sealed class RecordingHook : IEventHook
        {
            public Task Execute(EventPayload payload, CancellationToken cancellationToken = default)
                => Task.CompletedTask;
        }
    }
}
