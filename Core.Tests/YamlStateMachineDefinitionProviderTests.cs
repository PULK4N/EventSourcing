using EventSourcing.Core.Providers;
using EventSourcing.Shared.Containers;
using EventSourcing.Shared.Exceptions;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;

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
                projections:
                  - user-audit
                events:
                  YamlUserCreated:
                    uniqueConstraints:
                      - UniqueEmailConstraint
                      - UniqueUsernameConstraint
                    projections:
                      - user-search
                """
            );

            var provider = new YamlStateMachineDefinitionProvider(directoryPath);

            var definition = provider.Get("users-state-machine");
            Assert.Equal("YamlUserStateData", definition.StateData);
            Assert.Equal([ "user-audit" ], definition.Projections);
            var userCreated = definition.Events["YamlUserCreated"];
            Assert.Equal(
                [ "UniqueEmailConstraint", "UniqueUsernameConstraint" ],
                userCreated.UniqueConstraints
            );
            Assert.Equal([ "user-search" ], userCreated.Projections);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void ReusesRegisteredConstraintCreatorInstances()
    {
        var first = ConstraintCreatorTypeContainer.GetUniqueEventConstraintCreator(
            nameof(UniqueEmailConstraint)
        );
        var second = ConstraintCreatorTypeContainer.GetUniqueEventConstraintCreator(
            nameof(UniqueEmailConstraint)
        );

        Assert.Same(first, second);
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

public sealed class YamlUserStateData : ISharedStateData
{
    public Guid Id { get; set; }
    public bool IsDeleted { get; set; }
}

internal sealed class YamlUserCreated : IEvent
{
    public object Apply(object stateData, EventExecutionInfo eventExecutionInfo) => stateData;
}

public sealed class UniqueEmailConstraint : IUniqueConstraintCreator<YamlUserStateData>
{
    public IEnumerable<UniqueEventConstraintData> CreateConstraintsToRemove(
        YamlUserStateData stateBeforeEvent,
        EventPayload payload
    ) => [ new UniqueEventConstraintData("email", "removed-email") ];

    public IEnumerable<UniqueEventConstraintData> CreateConstraintsToAdd(
        YamlUserStateData stateAfterEvent,
        EventPayload payload
    ) => [ new UniqueEventConstraintData("email", "added-email") ];
}

public sealed class UniqueUsernameConstraint : IUniqueConstraintCreator<YamlUserStateData>
{
    public IEnumerable<UniqueEventConstraintData> CreateConstraintsToRemove(
        YamlUserStateData stateBeforeEvent,
        EventPayload payload
    ) => [ new UniqueEventConstraintData("username", "removed-username") ];

    public IEnumerable<UniqueEventConstraintData> CreateConstraintsToAdd(
        YamlUserStateData stateAfterEvent,
        EventPayload payload
    ) => [ new UniqueEventConstraintData("username", "added-username") ];
}
