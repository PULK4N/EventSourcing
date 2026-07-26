using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Models;
using EventSourcing.Core.Providers;
using EventSourcing.Shared.Containers;
using EventSourcing.Shared.Models;
using Moq;

namespace EventSourcing.Core.Tests;

[Collection(StaticTypeContainerCollection.Name)]
public sealed class EventValidatorProviderTests
{
    [Fact]
    public async Task GetsPreAndPostEventValidatorsFromStateMachineDefinition()
    {
        var payload = EventPayload.Create(
            EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
            AggregateId.FromDatabaseGuid(Guid.NewGuid()),
            "users-state-machine",
            new YamlUserCreated()
        );
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
                            PreEventValidators = [ nameof(YamlPreEventValidator) ],
                            PostEventValidators = [ nameof(YamlPostEventValidator) ]
                        }
                    }
                }
            );
        var provider = new EventValidatorProvider(definitions.Object);

        var preEventValidators = await provider.GetPreEventStateValidators(payload);
        var postEventValidators = await provider.GetPostEventStateValidators(payload);

        var preEventValidator = Assert.IsType<YamlPreEventValidator>(
            Assert.Single(preEventValidators)
        );
        var postEventValidator = Assert.IsType<YamlPostEventValidator>(
            Assert.Single(postEventValidators)
        );
        Assert.Same(
            EventValidatorContainer.GetEventValidator(nameof(YamlPreEventValidator)),
            preEventValidator
        );
        Assert.Same(
            EventValidatorContainer.GetEventValidator(nameof(YamlPostEventValidator)),
            postEventValidator
        );

        var validationResult = preEventValidator.Validate(
            new YamlUserStateData(),
            payload
        );
        Assert.Equal(payload.EventExecutionInfo.AggregateId, validationResult.AggregateId);
    }
}
