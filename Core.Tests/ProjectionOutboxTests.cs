using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Models;
using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;
using Moq;

namespace EventSourcing.Core.Tests;

public class ProjectionOutboxTests
{
    [Fact]
    public async Task RunsTopLevelProjectionForEventWithoutEventSpecificProjections()
    {
        var definition = new StateMachineDefinition
        {
            Id = "projection-tests",
            Projections = [ nameof(GlobalTestProjector) ],
            Events =
            {
                [nameof(ProjectionTestEvent)] = new StateMachineEventDefinition()
            }
        };
        var projector = new GlobalTestProjector();
        var outbox = CreateOutbox(definition, projector);
        var stateInfo = CreateStateInfo(definition.Id, new ProjectionTestEvent());

        await outbox.Write(new Dictionary<AggregateId, StateInfo>
        {
            [stateInfo.AggregateId] = stateInfo
        });

        var update = Assert.Single(projector.Updates);
        Assert.Same(stateInfo, Assert.Single(update));
    }

    [Fact]
    public async Task CombinesTopLevelAndEventSpecificProjectionsWithoutRunningDuplicates()
    {
        var definition = new StateMachineDefinition
        {
            Id = "projection-tests",
            Projections =
            [
                nameof(GlobalTestProjector),
                nameof(SharedTestProjector)
            ],
            Events =
            {
                [nameof(ProjectionTestEvent)] = new StateMachineEventDefinition
                {
                    Projections =
                    [
                        nameof(EventSpecificTestProjector),
                        nameof(SharedTestProjector)
                    ]
                }
            }
        };
        var globalProjector = new GlobalTestProjector();
        var eventProjector = new EventSpecificTestProjector();
        var sharedProjector = new SharedTestProjector();
        var outbox = CreateOutbox(
            definition,
            globalProjector,
            eventProjector,
            sharedProjector
        );
        var stateInfo = CreateStateInfo(definition.Id, new ProjectionTestEvent());

        await outbox.Write(new Dictionary<AggregateId, StateInfo>
        {
            [stateInfo.AggregateId] = stateInfo
        });

        Assert.Single(globalProjector.Updates);
        Assert.Single(eventProjector.Updates);
        Assert.Single(sharedProjector.Updates);
    }

    private static ProjectionOutbox CreateOutbox(
        StateMachineDefinition definition,
        params IProjector[] projectors
    )
    {
        var definitions = new Mock<IStateMachineDefinitionProvider>();
        definitions.Setup(x => x.Get(definition.Id)).Returns(definition);

        return new ProjectionOutbox(
            definitions.Object,
            null!,
            Mock.Of<IEventStore>(),
            projectors
        );
    }

    private static StateInfo CreateStateInfo(string stateMachineId, IEvent eventData)
    {
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var stateInfo = StateInfo.Create(new object(), stateMachineId, aggregateId);
        stateInfo.LastExecutedPayloads =
        [
            EventPayload.Create(
                EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
                aggregateId,
                stateMachineId,
                eventData
            )
        ];

        return stateInfo;
    }
}

internal sealed class ProjectionTestEvent : IEvent
{
    public object Apply(object stateData, EventExecutionInfo eventExecutionInfo) => stateData;
}

internal abstract class RecordingTestProjector : IProjector
{
    public List<List<StateInfo>> Updates { get; } = [ ];

    public Task Update(List<StateInfo> stateInfo)
    {
        Updates.Add(stateInfo);
        return Task.CompletedTask;
    }
}

internal sealed class GlobalTestProjector : RecordingTestProjector;

internal sealed class EventSpecificTestProjector : RecordingTestProjector;

internal sealed class SharedTestProjector : RecordingTestProjector;
