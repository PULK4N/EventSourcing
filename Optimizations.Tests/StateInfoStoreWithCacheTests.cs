using EventSourcing.Optimizations.Tests.TestModels;
using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Models;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Xunit;

namespace EventSourcing.Optimizations.Tests;

public sealed class StateInfoStoreWithCacheTests : IDisposable
{
    private readonly Mock<IEventStore> _eventStore = new();
    private readonly Dictionary<AggregateId, List<EventPayload>> _history = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly StateInfoStoreWithCache _store;

    public StateInfoStoreWithCacheTests()
    {
        _eventStore
            .Setup(store => store.GetEvents(It.IsAny<List<AggregateId>>()))
            .ReturnsAsync(
                (List<AggregateId> ids) =>
                    ids.ToDictionary(id => id, id => _history.GetValueOrDefault(id) ?? [ ])
            );

        _store = new StateInfoStoreWithCache(
            _eventStore.Object,
            CounterStateMachine.CreateCalculator(),
            _cache
        );
    }

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task Get_CalculatesStateFromEventsOnceAndThenReadsItFromCache()
    {
        var aggregateId = NewAggregateId();
        _history[aggregateId] =
        [
            CounterStateMachine.Payload(aggregateId, 1, 1),
            CounterStateMachine.Payload(aggregateId, 2, 2)
        ];

        var first = await _store.Get([ aggregateId ]);
        var second = await _store.Get([ aggregateId ]);

        Assert.Equal(3, Count(first[aggregateId]));
        Assert.Equal(2u, first[aggregateId].CurrentOrderNumber);
        Assert.Equal(3, Count(second[aggregateId]));
        Assert.Equal(2u, second[aggregateId].CurrentOrderNumber);
        _eventStore.Verify(store => store.GetEvents(It.IsAny<List<AggregateId>>()), Times.Once);
    }

    [Fact]
    public async Task Get_LeavesOutAggregateWithoutEvents()
    {
        var result = await _store.Get([ NewAggregateId() ]);

        Assert.Empty(result);
    }

    [Fact]
    public async Task Get_LoadsEventsOnlyForAggregatesMissingFromCache()
    {
        var cachedId = NewAggregateId();
        var missingId = NewAggregateId();
        await _store.Write([ CounterState(cachedId, 5) ]);
        _history[missingId] = [ CounterStateMachine.Payload(missingId, 7, 1) ];

        var result = await _store.Get([ cachedId, missingId ]);

        Assert.Equal(5, Count(result[cachedId]));
        Assert.Equal(7, Count(result[missingId]));
        _eventStore.Verify(
            store => store.GetEvents(It.Is<List<AggregateId>>(ids => ids.SequenceEqual(new[] { missingId }))),
            Times.Once
        );
    }

    [Fact]
    public async Task Write_StoresCopyWithUniqueConstraintsAndWithoutLastExecutedPayloads()
    {
        var aggregateId = NewAggregateId();
        var written = CounterState(aggregateId, 5);
        written.State = "COUNTING";
        written.CurrentOrderNumber = 4;
        written.UniqueConstraints = [ new CounterConstraint((CounterStateData)written.StateData) ];
        written.LastExecutedPayloads = [ CounterStateMachine.Payload(aggregateId, 5) ];
        await _store.Write([ written ]);

        ((CounterStateData)written.StateData).Count = 100;
        var first = (await _store.Get([ aggregateId ]))[aggregateId];
        Assert.Equal(5, Count(first));
        ((CounterStateData)first.StateData).Count = 200;
        var second = (await _store.Get([ aggregateId ]))[aggregateId];

        Assert.Equal(5, Count(second));
        Assert.Equal("COUNTING", second.State);
        Assert.Equal(4u, second.CurrentOrderNumber);
        Assert.Equal(CounterStateMachine.Id, second.StateMachineId);
        Assert.Equal(written.LastUpdateTimestamp, second.LastUpdateTimestamp);
        var constraint = Assert.Single(second.UniqueConstraints);
        Assert.Equal(written.UniqueConstraints[0].Hash, constraint.Hash);
        Assert.Equal(nameof(CounterConstraint), constraint.ConstraintName);
        Assert.Empty(second.LastExecutedPayloads);
    }

    private static StateInfo CounterState(AggregateId aggregateId, int count) =>
        StateInfo.Create(
            new CounterStateData(aggregateId) { Count = count },
            CounterStateMachine.Id,
            aggregateId
        );

    private static int Count(StateInfo stateInfo) =>
        Assert.IsType<CounterStateData>(stateInfo.StateData).Count;

    private static AggregateId NewAggregateId() => AggregateId.FromDatabaseGuid(Guid.NewGuid());
}
