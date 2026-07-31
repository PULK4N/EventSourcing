using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Models;
using EventSourcing.Core.Providers;
using EventSourcing.Shared.Models;
using Moq;

namespace EventSourcing.Core.Tests;

[Collection(StaticTypeContainerCollection.Name)]
public class StateMachineStateDataProviderTests
{
    [Fact]
    public async Task CreatesStateDataConfiguredForStateMachine()
    {
        var definitions = new Mock<IStateMachineDefinitionProvider>();
        definitions
            .Setup(provider => provider.Get("users-state-machine"))
            .Returns(
                new StateMachineDefinition
                {
                    Id = "users-state-machine",
                    StateData = nameof(YamlUserStateData)
                }
            );
        var provider = new StateMachineStateDataProvider(definitions.Object);
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());

        var stateData = await provider.GetStateDataByStateMachine(
            "users-state-machine",
            aggregateId
        );

        Assert.Equal(aggregateId, Assert.IsType<YamlUserStateData>(stateData).Id);
        definitions.Verify(
            definitionProvider => definitionProvider.Get("users-state-machine"),
            Times.Once
        );
    }
}
