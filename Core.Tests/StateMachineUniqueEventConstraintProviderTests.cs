using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Models;
using EventSourcing.Core.Providers;
using EventSourcing.Shared.Models;
using Moq;

namespace EventSourcing.Core.Tests;

[Collection(StaticTypeContainerCollection.Name)]
public class StateMachineUniqueEventConstraintProviderTests
{
    [Fact]
    public void ExecutesConfiguredConstraintCreatorsInDefinitionOrder()
    {
        var definitions = new Mock<IStateMachineDefinitionProvider>();
        definitions
            .Setup(provider => provider.Get("users-state-machine"))
            .Returns(
                new StateMachineDefinition
                {
                    Id = "users-state-machine",
                    StateData = nameof(YamlUserStateData),
                    Events =
                    {
                        [nameof(YamlUserCreated)] = new StateMachineEventDefinition
                        {
                            UniqueConstraints =
                            [
                                nameof(UniqueEmailConstraint),
                                nameof(UniqueUsernameConstraint)
                            ]
                        }
                    }
                }
            );
        var provider = new StateMachineUniqueEventConstraintProvider(definitions.Object);
        var payload = EventPayload.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "users-state-machine",
            new YamlUserCreated()
        );
        var stateData = new YamlUserStateData();

        var constraintsToRemove = provider
            .GetConstraintsToRemove(stateData, payload)
            .ToArray();
        var constraintsToAdd = provider.GetConstraintsToAdd(stateData, payload).ToArray();

        Assert.Equal(
            [ "removed-email", "removed-username" ],
            constraintsToRemove.Select(constraint => constraint.ValueToHash)
        );
        Assert.Equal(
            [ "added-email", "added-username" ],
            constraintsToAdd.Select(constraint => constraint.ValueToHash)
        );
    }

    [Fact]
    public void ReturnsNoConstraintsWhenEventIsNotConfigured()
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
        var provider = new StateMachineUniqueEventConstraintProvider(definitions.Object);
        var payload = EventPayload.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "users-state-machine",
            new YamlUserCreated()
        );

        Assert.Empty(provider.GetConstraintsToRemove(new YamlUserStateData(), payload));
        Assert.Empty(provider.GetConstraintsToAdd(new YamlUserStateData(), payload));
    }
}
