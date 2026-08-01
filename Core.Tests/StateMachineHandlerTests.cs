using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Providers;
using EventSourcing.Core.Tests.TestModels;
using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Exceptions;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;
using Moq;

namespace EventSourcing.Core.Tests;

public sealed class StateMachineHandlerTests
{
    private const string StateMachineId = "state-machine-handler-tests";

    private readonly Mock<IEventStoreWithOutbox> _eventStoreWithOutbox = new();
    private readonly StateMachineHandler _stateMachineHandler;

    public StateMachineHandlerTests()
    {
        var stateDataProvider = new Mock<IStateDataProvider>();
        stateDataProvider
            .Setup(
                provider =>
                    provider.GetStateDataByStateMachine(
                        StateMachineId,
                        It.IsAny<AggregateId>()
                    )
            )
            .ReturnsAsync(
                (string _, AggregateId aggregateId) =>
                    new AccountStateData(aggregateId) { Money = 100 }
            );

        var validatorProvider = new Mock<IEventValidatorProvider>();
        validatorProvider
            .Setup(provider => provider.GetPreEventStateValidators(It.IsAny<EventPayload>()))
            .ReturnsAsync([ ]);
        validatorProvider
            .Setup(provider => provider.GetPostEventStateValidators(It.IsAny<EventPayload>()))
            .ReturnsAsync([ ]);

        var constraintProvider = new Mock<IUniqueEventConstraintProvider>();
        constraintProvider
            .Setup(
                provider =>
                    provider.GetConstraintsToRemove(
                        It.IsAny<object>(),
                        It.IsAny<EventPayload>()
                    )
            )
            .Returns([ ]);
        constraintProvider
            .Setup(
                provider =>
                    provider.GetConstraintsToAdd(It.IsAny<object>(), It.IsAny<EventPayload>())
            )
            .Returns([ ]);

        _eventStoreWithOutbox
            .Setup(x => x.Write(It.IsAny<Dictionary<AggregateId, StateInfo>>()))
            .Returns(Task.CompletedTask);

        _stateMachineHandler = new StateMachineHandler(
            new StateCalculator(
                new OrderNumberHelper(),
                stateDataProvider.Object,
                validatorProvider.Object,
                constraintProvider.Object
            ),
            _eventStoreWithOutbox.Object
        );
    }

    [Fact(
        DisplayName = "ExecuteConditionalEvents throws when the event store retrieves an event with the conditional event order number"
    )]
    public async Task DuplicateOrderNumber()
    {
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var existingEvent = CreatePayload(
            aggregateId,
            new TransferMoney { MoneySent = 10 },
            1
        );
        var conditionalEvent = CreatePayload(
            aggregateId,
            new MultiplyMoney { Multiplier = 2 }
        );
        var conflictingEvent = CreatePayload(
            aggregateId,
            new TransferMoney { MoneySent = 20 },
            2
        );
        var generatedEvent = CreatePayload(
            aggregateId,
            new TransferMoney { MoneySent = 30 }
        );
        _eventStoreWithOutbox
            .SetupSequence(x => x.GetEvents(It.IsAny<List<AggregateId>>()))
            .ReturnsAsync(new Dictionary<AggregateId, List<EventPayload>>
            {
                [aggregateId] = [ existingEvent ]
            })
            .ReturnsAsync(new Dictionary<AggregateId, List<EventPayload>>
            {
                [aggregateId] = [ existingEvent, conflictingEvent ]
            });

        var exception = await Assert.ThrowsAsync<EventValidationException>(
            () =>
                _stateMachineHandler.ExecuteEvents(
                    conditionalEvent,
                    _ => [ generatedEvent ]
                )
        );

        Assert.Equal(2u, conditionalEvent.EventExecutionInfo.OrderNumber);
        Assert.Equal(
            Constants.DUPLICATE_ORDER_NUMBER_ON_OLD_EVENTS,
            Assert.Single(exception.ValidationResults).FailureReason
        );
        _eventStoreWithOutbox.Verify(
            x => x.Write(It.IsAny<Dictionary<AggregateId, StateInfo>>()),
            Times.Never
        );
    }

    [Fact(
        DisplayName = "ExecuteConditionalEvents returns the calculated conditional state info when no events are generated"
    )]
    public async Task NoGeneratedEvents()
    {
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var existingEvent = CreatePayload(
            aggregateId,
            new TransferMoney { MoneySent = 10 },
            1
        );
        var conditionalEvent = CreatePayload(
            aggregateId,
            new MultiplyMoney { Multiplier = 2 }
        );
        float? moneyReceivedByConditionalMethod = null;
        SetupEventStore(
            new Dictionary<AggregateId, List<EventPayload>>
            {
                [aggregateId] = [ existingEvent ]
            },
            [ ]
        );

        var stateInfos = await _stateMachineHandler.ExecuteEvents(
            conditionalEvent,
            stateInfo =>
            {
                moneyReceivedByConditionalMethod = ((AccountStateData)stateInfo.StateData).Money;
                return [ ];
            }
        );

        var stateInfo = Assert.Single(stateInfos).Value;
        Assert.Equal(180, moneyReceivedByConditionalMethod);
        Assert.Equal(aggregateId, stateInfo.AggregateId);
        Assert.Equal(StateMachineId, stateInfo.StateMachineId);
        Assert.Equal(2u, stateInfo.CurrentOrderNumber);
        Assert.Equal(conditionalEvent.EventExecutionInfo.Timestamp, stateInfo.LastUpdateTimestamp);
        Assert.Equal(180, Assert.IsType<AccountStateData>(stateInfo.StateData).Money);
        Assert.Equal([ conditionalEvent ], stateInfo.LastExecutedPayloads);
    }

    [Fact(
        DisplayName = "ExecuteConditionalEvents calculates the conditional state with later events for the same aggregate"
    )]
    public async Task EventsForConditionalAggregate()
    {
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var existingEvent = CreatePayload(
            aggregateId,
            new TransferMoney { MoneySent = 10 },
            1
        );
        var conditionalEvent = CreatePayload(
            aggregateId,
            new MultiplyMoney { Multiplier = 2 }
        );
        var transferEvent = CreatePayload(
            aggregateId,
            new TransferMoney { MoneySent = 30 }
        );
        var multiplyEvent = CreatePayload(
            aggregateId,
            new MultiplyMoney { Multiplier = 2 }
        );
        var existingEvents = new Dictionary<AggregateId, List<EventPayload>>
        {
            [aggregateId] = [ existingEvent ]
        };
        SetupEventStore(existingEvents, existingEvents);

        var stateInfos = await _stateMachineHandler.ExecuteEvents(
            conditionalEvent,
            _ => [ transferEvent, multiplyEvent ]
        );

        var stateInfo = Assert.Single(stateInfos).Value;
        Assert.Equal(aggregateId, stateInfo.AggregateId);
        Assert.Equal(StateMachineId, stateInfo.StateMachineId);
        Assert.Equal(4u, stateInfo.CurrentOrderNumber);
        Assert.Equal(multiplyEvent.EventExecutionInfo.Timestamp, stateInfo.LastUpdateTimestamp);
        Assert.Equal(300, Assert.IsType<AccountStateData>(stateInfo.StateData).Money);
        Assert.Equal(
            [ conditionalEvent, transferEvent, multiplyEvent ],
            stateInfo.LastExecutedPayloads
        );
    }

    [Fact(
        DisplayName = "ExecuteConditionalEvents writes calculated state infos and their executed events for all aggregates"
    )]
    public async Task EventsForMultipleAggregates()
    {
        var conditionalAggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var otherAggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var conditionalExistingEvent = CreatePayload(
            conditionalAggregateId,
            new TransferMoney { MoneySent = 10 },
            1
        );
        var otherExistingEvent = CreatePayload(
            otherAggregateId,
            new TransferMoney { MoneySent = 5 },
            1
        );
        var conditionalEvent = CreatePayload(
            conditionalAggregateId,
            new MultiplyMoney { Multiplier = 2 }
        );
        var conditionalAggregateEvent = CreatePayload(
            conditionalAggregateId,
            new TransferMoney { MoneySent = 30 }
        );
        var otherAggregateEvent = CreatePayload(
            otherAggregateId,
            new MultiplyMoney { Multiplier = 2 }
        );
        SetupEventStore(
            new Dictionary<AggregateId, List<EventPayload>>
            {
                [conditionalAggregateId] = [ conditionalExistingEvent ]
            },
            new Dictionary<AggregateId, List<EventPayload>>
            {
                [conditionalAggregateId] = [ conditionalExistingEvent ],
                [otherAggregateId] = [ otherExistingEvent ]
            }
        );

        var stateInfos = await _stateMachineHandler.ExecuteEvents(
            conditionalEvent,
            _ => [ conditionalAggregateEvent, otherAggregateEvent ]
        );

        Assert.Equal(2, stateInfos.Count);
        var conditionalStateInfo = stateInfos[conditionalAggregateId];
        Assert.Equal(3u, conditionalStateInfo.CurrentOrderNumber);
        Assert.Equal(150, Assert.IsType<AccountStateData>(conditionalStateInfo.StateData).Money);
        Assert.Equal(
            [ conditionalEvent, conditionalAggregateEvent ],
            conditionalStateInfo.LastExecutedPayloads
        );

        var otherStateInfo = stateInfos[otherAggregateId];
        Assert.Equal(2u, otherStateInfo.CurrentOrderNumber);
        Assert.Equal(190, Assert.IsType<AccountStateData>(otherStateInfo.StateData).Money);
        Assert.Equal([ otherAggregateEvent ], otherStateInfo.LastExecutedPayloads);

        _eventStoreWithOutbox.Verify(x => x.Write(stateInfos), Times.Once);
    }

    private void SetupEventStore(
        Dictionary<AggregateId, List<EventPayload>> firstRead,
        Dictionary<AggregateId, List<EventPayload>> secondRead
    ) =>
        _eventStoreWithOutbox
            .SetupSequence(x => x.GetEvents(It.IsAny<List<AggregateId>>()))
            .ReturnsAsync(firstRead)
            .ReturnsAsync(secondRead);

    private static EventPayload CreatePayload(
        AggregateId aggregateId,
        IEvent eventData,
        uint orderNumber = 0
    )
    {
        var payload = EventPayload.Create(
            EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
            aggregateId,
            StateMachineId,
            eventData
        );
        payload.EventExecutionInfo.OrderNumber = orderNumber;
        return payload;
    }
}
