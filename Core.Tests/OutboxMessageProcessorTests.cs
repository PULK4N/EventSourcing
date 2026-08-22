using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Models;
using EventSourcing.Core.Providers;
using EventSourcing.Core.Tests.TestModels;
using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace EventSourcing.Core.Tests;

public sealed class OutboxMessageProcessorTests
{
    private const string StateMachineId = "outbox-processor-tests";

    [Fact]
    public async Task ProcessNextReplaysOnlyThroughMessageAndCompletesIt()
    {
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var firstPayload = CreatePayload(aggregateId, 1, 10);
        var laterPayload = CreatePayload(aggregateId, 2, 20);
        var outboxMessage = new MessagePayload
        {
            Id = 42,
            Payload = firstPayload,
            ExecutionAttempts = 1
        };
        var outbox = new Mock<IOutbox>();
        outbox.Setup(x => x.ReadNext(It.IsAny<CancellationToken>()))
            .ReturnsAsync(outboxMessage);
        var eventStore = new Mock<IEventStore>();
        eventStore.Setup(x => x.GetEvents(It.IsAny<List<AggregateId>>()))
            .ReturnsAsync(
                new Dictionary<AggregateId, List<EventPayload>>
                {
                    [aggregateId] = [ firstPayload, laterPayload ]
                }
            );
        var projector = new ProcessorRecordingProjector();
        var processor = CreateProcessor(outbox, eventStore, projector);

        var processed = await processor.ProcessNext();

        Assert.True(processed);
        var projectedState = Assert.Single(Assert.Single(projector.Updates));
        Assert.Equal(90, Assert.IsType<AccountStateData>(projectedState.StateData).Money);
        Assert.Same(firstPayload, Assert.Single(projectedState.LastExecutedPayloads));
        outbox.Verify(x => x.UpdateCompleted(42), Times.Once);
        outbox.Verify(x => x.UpdateFailed(It.IsAny<long>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ProcessNextReturnsMessageForRetryWhenProjectionFails()
    {
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var payload = CreatePayload(aggregateId, 1, 10);
        var outbox = new Mock<IOutbox>();
        outbox.Setup(x => x.ReadNext(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new MessagePayload
                {
                    Id = 84,
                    Payload = payload,
                    ExecutionAttempts = 2
                }
            );
        var eventStore = new Mock<IEventStore>();
        eventStore.Setup(x => x.GetEvents(It.IsAny<List<AggregateId>>()))
            .ReturnsAsync(
                new Dictionary<AggregateId, List<EventPayload>>
                {
                    [aggregateId] = [ payload ]
                }
            );
        var processor = CreateProcessor(outbox, eventStore, new ThrowingProcessorProjector());

        var processed = await processor.ProcessNext();

        Assert.True(processed);
        outbox.Verify(
            x =>
                x.UpdateFailed(
                    84,
                    It.Is<string>(error => error.Contains("Projection failed."))
                ),
            Times.Once
        );
        outbox.Verify(x => x.UpdateCompleted(It.IsAny<long>()), Times.Never);
    }

    private static OutboxMessageProcessor CreateProcessor(
        Mock<IOutbox> outbox,
        Mock<IEventStore> eventStore,
        IProjector projector
    )
    {
        var stateCalculator = CreateStateCalculator();
        var definitions = new Mock<IStateMachineDefinitionProvider>();
        definitions.Setup(x => x.Get(StateMachineId))
            .Returns(
                new StateMachineDefinition
                {
                    Id = StateMachineId,
                    Projections = [ projector.GetType().Name ],
                    Events =
                    {
                        [nameof(TransferMoney)] = new StateMachineEventDefinition()
                    }
                }
            );
        var projectionOutbox = new ProjectionOutbox(
            definitions.Object,
            stateCalculator,
            eventStore.Object,
            [ projector ]
        );

        return new OutboxMessageProcessor(
            outbox.Object,
            eventStore.Object,
            stateCalculator,
            projectionOutbox,
            Mock.Of<ILogger<OutboxMessageProcessor>>()
        );
    }

    private static StateCalculator CreateStateCalculator()
    {
        var stateDataProvider = new Mock<IStateDataProvider>();
        stateDataProvider.Setup(
                provider => provider.GetStateDataByStateMachine(
                    StateMachineId,
                    It.IsAny<AggregateId>()
                )
            )
            .ReturnsAsync(
                (string _, AggregateId aggregateId) =>
                    new AccountStateData(aggregateId) { Money = 100 }
            );
        var validatorProvider = new Mock<IEventValidatorProvider>();
        validatorProvider.Setup(
                provider => provider.GetPreEventStateValidators(It.IsAny<EventPayload>())
            )
            .ReturnsAsync([ ]);
        validatorProvider.Setup(
                provider => provider.GetPostEventStateValidators(It.IsAny<EventPayload>())
            )
            .ReturnsAsync([ ]);
        var constraintProvider = new Mock<IUniqueEventConstraintProvider>();
        constraintProvider.Setup(
                provider => provider.GetConstraintsToRemove(
                    It.IsAny<object>(),
                    It.IsAny<EventPayload>()
                )
            )
            .Returns([ ]);
        constraintProvider.Setup(
                provider => provider.GetConstraintsToAdd(
                    It.IsAny<object>(),
                    It.IsAny<EventPayload>()
                )
            )
            .Returns([ ]);
        var stateMachineDefinitionsProvider = new Mock<IStateMachineDefinitionProvider>();
        stateMachineDefinitionsProvider
            .Setup(provider => provider.Get(StateMachineId))
            .Returns(new StateMachineDefinition { Id = StateMachineId });

        return new StateCalculator(
            new OrderNumberHelper(),
            stateDataProvider.Object,
            validatorProvider.Object,
            constraintProvider.Object,
            stateMachineDefinitionsProvider.Object
        );
    }

    private static EventPayload CreatePayload(
        AggregateId aggregateId,
        uint orderNumber,
        int moneySent
    )
    {
        var payload = EventPayload.Create(
            EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
            aggregateId,
            StateMachineId,
            new TransferMoney { MoneySent = moneySent }
        );
        payload.EventExecutionInfo.OrderNumber = orderNumber;
        return payload;
    }
}

internal sealed class ProcessorRecordingProjector : IProjector
{
    public List<List<StateInfo>> Updates { get; } = [ ];

    public Task Update(List<StateInfo> stateInfo)
    {
        Updates.Add(stateInfo);
        return Task.CompletedTask;
    }
}

internal sealed class ThrowingProcessorProjector : IProjector
{
    public Task Update(List<StateInfo> stateInfo) =>
        throw new InvalidOperationException("Projection failed.");
}
