using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Models;
using EventSourcing.Core.Providers;
using EventSourcing.Core.Tests.TestModels;
using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Exceptions;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;
using Moq;
using Microsoft.EntityFrameworkCore;

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
            .Returns(
                (string _, AggregateId aggregateId) =>
                    new AccountStateData(aggregateId) { Money = 100 }
            );

        var validatorProvider = new Mock<IEventValidatorProvider>();
        validatorProvider
            .Setup(provider => provider.GetPreEventStateValidators(It.IsAny<EventPayload>()))
            .Returns([ ]);
        validatorProvider
            .Setup(provider => provider.GetPostEventStateValidators(It.IsAny<EventPayload>()))
            .Returns([ ]);

        _eventStoreWithOutbox
            .Setup(x => x.Write(It.IsAny<Dictionary<AggregateId, StateInfo>>()))
            .Returns(Task.CompletedTask);
        var stateMachineDefinitionsProvider = new Mock<IStateMachineDefinitionProvider>();
        stateMachineDefinitionsProvider
            .Setup(provider => provider.Get(StateMachineId))
            .Returns(new StateMachineDefinition { Id = StateMachineId });

        _stateMachineHandler = new StateMachineHandler(
            new StateCalculator(
                new OrderNumberHelper(),
                stateDataProvider.Object,
                validatorProvider.Object,
                stateMachineDefinitionsProvider.Object
            ),
            _eventStoreWithOutbox.Object
        );
    }

    [Fact(
        DisplayName = "ExecuteConditionalEvents propagates a write conflict without reloading history"
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
        var generatedEvent = CreatePayload(
            aggregateId,
            new TransferMoney { MoneySent = 30 }
        );
        SetupEventStore(new Dictionary<AggregateId, List<EventPayload>>
        {
            [aggregateId] = [ existingEvent ]
        });
        var writeConflict = new DbUpdateException("Duplicate aggregate order number.");
        _eventStoreWithOutbox
            .Setup(x => x.Write(It.IsAny<Dictionary<AggregateId, StateInfo>>()))
            .ThrowsAsync(writeConflict);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () =>
                _stateMachineHandler.ExecuteEvents(
                    conditionalEvent,
                    _ => [ generatedEvent ]
                )
        );

        Assert.Equal(2u, conditionalEvent.EventExecutionInfo.OrderNumber);
        Assert.Same(writeConflict, exception);
        _eventStoreWithOutbox.Verify(
            x => x.GetEvents(It.IsAny<List<AggregateId>>()),
            Times.Once
        );
        _eventStoreWithOutbox.Verify(
            x => x.Write(It.IsAny<Dictionary<AggregateId, StateInfo>>()),
            Times.Once
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
            }
        );

        var stateInfos = await _stateMachineHandler.ExecuteEvents(
            conditionalEvent,
            stateInfo =>
            {
                moneyReceivedByConditionalMethod =
                    ((AccountStateData)Assert.Single(stateInfo).StateData).Money;
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
        SetupEventStore(existingEvents);

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
        _eventStoreWithOutbox.Verify(
            x => x.GetEvents(It.IsAny<List<AggregateId>>()),
            Times.Once
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
        _eventStoreWithOutbox.Verify(
            x => x.GetEvents(It.Is<List<AggregateId>>(ids =>
                ids.Count == 1 && ids[0] == conditionalAggregateId)),
            Times.Once
        );
        _eventStoreWithOutbox.Verify(
            x => x.GetEvents(It.Is<List<AggregateId>>(ids =>
                ids.Count == 1 && ids[0] == otherAggregateId)),
            Times.Once
        );
        _eventStoreWithOutbox.Verify(
            x => x.GetEvents(It.IsAny<List<AggregateId>>()),
            Times.Exactly(2)
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task MultipleInitialEventsAndChainedCallbacks(int callbackCount)
    {
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var otherAggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        SetupEventStore(new Dictionary<AggregateId, List<EventPayload>>
        {
            [aggregateId] = [ CreatePayload(aggregateId, new TransferMoney { MoneySent = 10 }, 1) ],
            [otherAggregateId] = [ CreatePayload(otherAggregateId, new TransferMoney { MoneySent = 5 }, 1) ]
        });
        var initialEvents = new List<EventPayload>
        {
            CreatePayload(aggregateId, new MultiplyMoney { Multiplier = 2 }),
            CreatePayload(aggregateId, new TransferMoney { MoneySent = 10 }),
            CreatePayload(otherAggregateId, new MultiplyMoney { Multiplier = 2 })
        };
        var expectedEvents = new List<EventPayload>(initialEvents);
        var observedBalances = new List<float>();
        var callbacks = new List<Func<StateInfo[], List<EventPayload>>>();
        for (var index = 0; index < callbackCount; index++)
        {
            callbacks.Add(states =>
            {
                Assert.Equal(2, states.Length);
                var state = Assert.Single(states, state => state.AggregateId == aggregateId);
                observedBalances.Add(Assert.IsType<AccountStateData>(state.StateData).Money);
                var generatedEvent = CreatePayload(aggregateId, new TransferMoney { MoneySent = 10 });
                expectedEvents.Add(generatedEvent);
                return [ generatedEvent ];
            });
        }

        var results = await _stateMachineHandler.ExecuteEvents(initialEvents, callbacks.ToArray());

        Assert.Equal(
            Enumerable.Range(0, callbackCount).Select(index => 170f - index * 10),
            observedBalances
        );
        Assert.Equal(2, results.Count);
        Assert.Equal(170f - callbackCount * 10, Assert.IsType<AccountStateData>(results[aggregateId].StateData).Money);
        Assert.Equal(190f, Assert.IsType<AccountStateData>(results[otherAggregateId].StateData).Money);
        Assert.Equal((uint)(3 + callbackCount), results[aggregateId].CurrentOrderNumber);
        Assert.Equal(2u, results[otherAggregateId].CurrentOrderNumber);
        foreach (var (id, state) in results)
        {
            Assert.Equal(
                expectedEvents.Where(payload => payload.EventExecutionInfo.AggregateId == id),
                state.LastExecutedPayloads
            );
            Assert.Equal(
                Enumerable.Range(2, state.LastExecutedPayloads.Count).Select(order => (uint)order),
                state.LastExecutedPayloads.Select(payload => payload.EventExecutionInfo.OrderNumber)
            );
        }
        _eventStoreWithOutbox.Verify(
            store => store.GetEvents(It.Is<List<AggregateId>>(ids =>
                ids.Count == 2 && ids.Contains(aggregateId) && ids.Contains(otherAggregateId))),
            Times.Once
        );
        _eventStoreWithOutbox.Verify(store => store.GetEvents(It.IsAny<List<AggregateId>>()), Times.Once);
        _eventStoreWithOutbox.Verify(store => store.Write(results), Times.Once);
    }

    private void SetupEventStore(
        Dictionary<AggregateId, List<EventPayload>> history
    ) =>
        _eventStoreWithOutbox
            .Setup(x => x.GetEvents(It.IsAny<List<AggregateId>>()))
            .ReturnsAsync((List<AggregateId> ids) => ids.ToDictionary(
                id => id,
                id => history.GetValueOrDefault(id) ?? new List<EventPayload>()
            ));

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
