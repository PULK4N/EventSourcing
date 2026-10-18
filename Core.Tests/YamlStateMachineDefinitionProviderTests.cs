using EventSourcing.Core.Providers;
using EventSourcing.Shared.Exceptions;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;
using Shared.Interfaces;

namespace EventSourcing.Core.Tests;

[Collection(StaticTypeContainerCollection.Name)]
public class YamlStateMachineDefinitionProviderTests
{
    [Fact]
    public void LoadsStateMachineDefinitionFromYaml()
    {
        var directoryPath = CreateTemporaryDirectory();

        try
        {
            File.WriteAllText(
                Path.Combine(directoryPath, "users.yaml"),
                """
                id: users-state-machine
                stateData: YamlUserStateData
                initializationEvents:
                  - YamlUserCreated
                projections:
                  - user-audit
                events:
                  YamlUserCreated:
                    preEventValidators:
                      - YamlPreEventValidator
                    postEventValidators:
                      - YamlPostEventValidator
                    projections:
                      - user-search
                """
            );

            var provider = new YamlStateMachineDefinitionProvider(directoryPath);

            var definition = provider.Get("users-state-machine");
            Assert.Equal("YamlUserStateData", definition.StateData);
            Assert.Equal([ "YamlUserCreated" ], definition.InitializationEvents);
            Assert.Equal([ "user-audit" ], definition.Projections);
            var userCreated = definition.Events["YamlUserCreated"];
            Assert.Equal([ "YamlPreEventValidator" ], userCreated.PreEventValidators);
            Assert.Equal([ "YamlPostEventValidator" ], userCreated.PostEventValidators);
            Assert.Equal([ "user-search" ], userCreated.Projections);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void RejectsDuplicateStateMachineIds()
    {
        var directoryPath = CreateTemporaryDirectory();

        try
        {
            const string yaml = """
                id: users-state-machine
                stateData: YamlUserStateData
                """;
            File.WriteAllText(Path.Combine(directoryPath, "first.yaml"), yaml);
            File.WriteAllText(Path.Combine(directoryPath, "second.yml"), yaml);

            var exception = Assert.Throws<InvalidOperationException>(
                () => new YamlStateMachineDefinitionProvider(directoryPath)
            );

            Assert.Contains("defined more than once", exception.Message);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void RejectsUnregisteredStateData()
    {
        var directoryPath = CreateTemporaryDirectory();

        try
        {
            File.WriteAllText(
                Path.Combine(directoryPath, "users.yaml"),
                """
                id: users-state-machine
                stateData: MissingStateData
                """
            );

            var exception = Assert.Throws<StateDataTypeNotFoundException>(
                () => new YamlStateMachineDefinitionProvider(directoryPath)
            );

            Assert.Contains("MissingStateData", exception.Message);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void RejectsUnregisteredEvent()
    {
        var directoryPath = CreateTemporaryDirectory();

        try
        {
            File.WriteAllText(
                Path.Combine(directoryPath, "users.yaml"),
                """
                id: users-state-machine
                stateData: YamlUserStateData
                events:
                  MissingEvent: {}
                """
            );

            var exception = Assert.Throws<EventNotRegisteredException>(
                () => new YamlStateMachineDefinitionProvider(directoryPath)
            );

            Assert.Contains("MissingEvent", exception.Message);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void RejectsUnregisteredInitializationEvent()
    {
        var directoryPath = CreateTemporaryDirectory();

        try
        {
            File.WriteAllText(
                Path.Combine(directoryPath, "users.yaml"),
                """
                id: users-state-machine
                stateData: YamlUserStateData
                initializationEvents:
                  - MissingEvent
                """
            );

            var exception = Assert.Throws<EventNotRegisteredException>(
                () => new YamlStateMachineDefinitionProvider(directoryPath)
            );

            Assert.Contains("MissingEvent", exception.Message);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Theory]
    [InlineData("{}", 0, false)]
    [InlineData("{hooks: null}", 0, false)]
    [InlineData("{hooks: []}", 0, false)]
    [InlineData("{hooks: [NotifyHook, NotifyHook, notifyhook]}", 2, false)]
    [InlineData("{hooks: ['']}", 0, true)]
    [InlineData("{hooks: [' ']}", 0, true)]
    [InlineData("{hooks: [null]}", 0, true)]
    public void LoadsHookSetsAndRejectsBlankNames(
        string eventDefinition, int expectedCount, bool shouldThrow)
    {
        var directoryPath = CreateTemporaryDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(directoryPath, "hooks.yaml"),
                $"""
                id: users-state-machine
                stateData: YamlUserStateData
                events:
                  YamlUserCreated: {eventDefinition}
                """
            );

            if (shouldThrow)
            {
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    new YamlStateMachineDefinitionProvider(directoryPath));
                Assert.Contains("empty hook name", exception.Message);
                Assert.Contains("users-state-machine", exception.Message);
                Assert.Contains(nameof(YamlUserCreated), exception.Message);
            }
            else
            {
                var provider = new YamlStateMachineDefinitionProvider(directoryPath);
                var hooks = provider.Get("users-state-machine")
                    .Events[nameof(YamlUserCreated)].Hooks;
                Assert.Equal(expectedCount, hooks.Count);
                if (expectedCount > 0)
                    Assert.True(hooks.SetEquals(["NotifyHook", "notifyhook"]));
            }
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"event-sourcing-tests-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }
}

public sealed class YamlUserStateData(AggregateId aggregateId) : ISharedStateData
{
    public AggregateId Id { get; init; } = aggregateId;
    public bool IsDeleted { get; set; }
}

internal sealed class YamlUserCreated : IEvent
{
    public object Apply(object stateData, EventExecutionInfo eventExecutionInfo) => stateData;
}

public sealed class YamlPreEventValidator : IPreEventValidator
{
    public EventValidationResult Validate(object stateData, EventPayload payload) =>
        EventValidationResult.FromPayload(payload, nameof(YamlPreEventValidator), true, null);
}

public sealed class YamlPostEventValidator : IPostEventValidator
{
    public EventValidationResult Validate(object stateData, EventPayload payload) =>
        EventValidationResult.FromPayload(payload, nameof(YamlPostEventValidator), true, null);
}
