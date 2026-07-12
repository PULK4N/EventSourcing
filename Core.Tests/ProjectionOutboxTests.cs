using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Models;
using EventSourcing.Core.Providers;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;
using Moq;

namespace EventSourcing.Core.Tests;

public class ProjectionOutboxTests
{
    [Fact]
    public async Task ExecutesConfiguredProjectorsOnceWithMatchingEvents()
    {
        var aggregateId = Guid.NewGuid();
        var firstPayload = CreatePayload(aggregateId);
        var secondPayload = CreatePayload(aggregateId);
        var definitions = CreateDefinitionProvider(
            new StateMachineDefinition
            {
                Id = "users",
                StateData = "UserStateData",
                Projections = [ nameof(AllUsersProjector), nameof(SharedProjector) ],
                Events =
                {
                    [nameof(TestEvent)] = new StateMachineEventDefinition
                    {
                        Projections = [ nameof(SharedProjector), nameof(UserEventProjector) ]
                    }
                }
            }
        );
        var allUsers = new AllUsersProjector();
        var shared = new SharedProjector();
        var userEvent = new UserEventProjector();
        var outbox = new ProjectionOutbox(
            definitions.Object,
            [ allUsers, shared, userEvent ]
        );

        await outbox.Write(firstPayload, secondPayload);

        Assert.Equal([ firstPayload, secondPayload ], Assert.Single(allUsers.Calls));
        Assert.Equal([ firstPayload, secondPayload ], Assert.Single(shared.Calls));
        Assert.Equal([ firstPayload, secondPayload ], Assert.Single(userEvent.Calls));
    }

    [Fact]
    public async Task ThrowsWhenConfiguredProjectorIsNotRegistered()
    {
        var aggregateId = Guid.NewGuid();
        var payload = CreatePayload(aggregateId);
        var definitions = CreateDefinitionProvider(
            new StateMachineDefinition
            {
                Id = "users",
                StateData = "UserStateData",
                Projections = [ "MissingProjector" ]
            }
        );
        var outbox = new ProjectionOutbox(definitions.Object, [ ]);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            outbox.Write(payload)
        );

        Assert.Contains("MissingProjector", exception.Message);
    }

    private static Mock<IStateMachineDefinitionProvider> CreateDefinitionProvider(
        StateMachineDefinition definition
    )
    {
        var provider = new Mock<IStateMachineDefinitionProvider>(MockBehavior.Strict);
        provider.Setup(x => x.Get(definition.Id)).Returns(definition);
        return provider;
    }

    private static EventPayload CreatePayload(Guid aggregateId) =>
        EventPayload.Create(Guid.NewGuid(), aggregateId, "users", new TestEvent());

    private abstract class RecordingProjector : IEventProjector
    {
        public List<EventPayload[]> Calls { get; } = [ ];

        public Task Update(params EventPayload[] payloads)
        {
            Calls.Add(payloads);
            return Task.CompletedTask;
        }
    }

    private sealed class AllUsersProjector : RecordingProjector { }

    private sealed class SharedProjector : RecordingProjector { }

    private sealed class UserEventProjector : RecordingProjector { }

    private sealed class TestEvent : IEvent
    {
        public object Apply(object stateData, EventExecutionInfo eventExecutionInfo) => stateData;
    }
}
