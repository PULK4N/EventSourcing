using EventSourcing.Optimizations.Tests.TestModels;
using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Models;
using Moq;
using Xunit;

namespace EventSourcing.Optimizations.Tests;

public sealed class StateMachineHandlerWithStateStoreTests
{
    private readonly Mock<IEventStoreWithOutbox> _eventStoreWithOutbox = new();
    private readonly Mock<IStateInfoStore> _stateInfoStore = new();
    private readonly List<string> _writeOrder = [ ];
    private readonly StateMachineHandlerWithStateStore _handler;

    public StateMachineHandlerWithStateStoreTests()
    {
        _eventStoreWithOutbox
            .Setup(store => store.GetEvents(It.IsAny<List<AggregateId>>()))
            .ReturnsAsync((List<AggregateId> ids) => ids.ToDictionary(id => id, _ => new List<EventPayload>()));
        _eventStoreWithOutbox
            .Setup(store => store.Write(It.IsAny<Dictionary<AggregateId, StateInfo>>()))
            .Callback(() => _writeOrder.Add(nameof(IEventStoreWithOutbox)))
            .Returns(Task.CompletedTask);
        _stateInfoStore
            .Setup(store => store.Write(It.IsAny<List<StateInfo>>()))
            .Callback(() => _writeOrder.Add(nameof(IStateInfoStore)))
            .Returns(Task.CompletedTask);

        _handler = new StateMachineHandlerWithStateStore(
            CounterStateMachine.CreateCalculator(),
            _stateInfoStore.Object,
            _eventStoreWithOutbox.Object
        );
    }

    [Fact]
    public async Task ExecuteEvents_WritesStateToStoreAfterEventsAreSaved()
    {
        var aggregateId = NewAggregateId();

        var result = await _handler.ExecuteEvents(CounterStateMachine.Payload(aggregateId, 3));

        Assert.Equal([ nameof(IEventStoreWithOutbox), nameof(IStateInfoStore) ], _writeOrder);
        _stateInfoStore.Verify(
            store => store.Write(It.Is<List<StateInfo>>(states => states.Single() == result)),
            Times.Once
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteEvents_DoesNotWriteToStoreWhenExecutionFails(bool failInConditionalMethod)
    {
        var failure = new InvalidOperationException();
        if (!failInConditionalMethod)
            _eventStoreWithOutbox
                .Setup(store => store.Write(It.IsAny<Dictionary<AggregateId, StateInfo>>()))
                .ThrowsAsync(failure);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                _handler.ExecuteEvents(
                    CounterStateMachine.Payload(NewAggregateId(), 3),
                    _ => failInConditionalMethod ? throw failure : [ ]
                )
        );

        Assert.Same(failure, exception);
        _stateInfoStore.Verify(store => store.Write(It.IsAny<List<StateInfo>>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetByAggregateId_ReadsOnlyFromStateStore(bool isStored)
    {
        var aggregateId = NewAggregateId();
        var stored = StateInfo.Create(new CounterStateData(aggregateId), CounterStateMachine.Id, aggregateId);
        _stateInfoStore
            .Setup(store => store.Get(It.Is<List<AggregateId>>(ids => ids.SequenceEqual(new[] { aggregateId }))))
            .ReturnsAsync(
                isStored
                    ? new Dictionary<AggregateId, StateInfo> { [aggregateId] = stored }
                    : new Dictionary<AggregateId, StateInfo>()
            );

        var result = await _handler.GetByAggregateId(aggregateId);

        Assert.Equal(isStored ? stored : null, result);
        _eventStoreWithOutbox.Verify(store => store.GetEvents(It.IsAny<List<AggregateId>>()), Times.Never);
    }

    private static AggregateId NewAggregateId() => AggregateId.FromDatabaseGuid(Guid.NewGuid());
}
