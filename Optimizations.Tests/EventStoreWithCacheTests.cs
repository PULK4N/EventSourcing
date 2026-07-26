using System.Text.Json;
using EventSourcing.Persistence;
using EventSourcing.Persistence.Models;
using EventSourcing.Shared.Containers;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace EventSourcing.Optimizations.Tests;

public class EventStoreWithCacheTests : IDisposable
{
    private readonly EventSourcingDbContext _dbContext;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly EventStoreWithCache _eventStore;

    static EventStoreWithCacheTests()
    {
        EventTypeContainer.AddEventType(typeof(TestEvent));
    }

    public EventStoreWithCacheTests()
    {
        var options = new DbContextOptionsBuilder<EventSourcingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new EventSourcingDbContext(options);
        _eventStore = new EventStoreWithCache(new BaseSqlEventStore(_dbContext), _cache);
    }

    [Fact]
    public async Task GetEvents_LoadsEventsFromSqlAndStoresThemInCache()
    {
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var firstEvent = CreateEvent(aggregateId, 1);
        var secondEvent = CreateEvent(aggregateId, 2);
        await Seed(firstEvent, secondEvent);

        var events = new List<EventPayload>() { firstEvent, secondEvent };
        var serializedEvents = JsonSerializer.Serialize(events, new JsonSerializerOptions());
        var result = await _eventStore.GetEvents(aggregateId);
        var resultValues = result.SelectMany(x => x.Value).ToList();
        var serializedResultValues = JsonSerializer.Serialize(
            resultValues,
            new JsonSerializerOptions()
        );
        Assert.Equal(serializedEvents, serializedResultValues);

        Assert.True(
            _cache.TryGetValue<EventPayload[]>(GetCacheKey(aggregateId), out var cachedEvents)
        );
        Assert.Equal(2, cachedEvents!.Length);
    }

    [Fact]
    public async Task GetEvents_AppendsEventsAddedAfterAggregateWasCached()
    {
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var firstEvent = CreateEvent(aggregateId, 1);
        await Seed(firstEvent);
        await _eventStore.GetEvents(aggregateId);

        var secondEvent = CreateEvent(aggregateId, 2);
        await Seed(secondEvent);

        var result = await _eventStore.GetEvents(aggregateId);

        Assert.Equal(
            [ 1u, 2u ],
            result[aggregateId].Select(payload => payload.EventExecutionInfo.OrderNumber)
        );
        Assert.True(
            _cache.TryGetValue<EventPayload[]>(GetCacheKey(aggregateId), out var cachedEvents)
        );
        Assert.Equal(2, cachedEvents!.Length);
    }

    [Fact]
    public async Task GetEvents_ReturnsEventsForEachDistinctAggregate()
    {
        var firstAggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var secondAggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        await Seed(CreateEvent(firstAggregateId, 1), CreateEvent(secondAggregateId, 1));

        var result = await _eventStore.GetEvents(
            firstAggregateId,
            secondAggregateId,
            firstAggregateId
        );

        Assert.Equal(2, result.Count);
        Assert.Single(result[firstAggregateId]);
        Assert.Single(result[secondAggregateId]);
        Assert.Equal(firstAggregateId, result[firstAggregateId][0].EventExecutionInfo.AggregateId);
        Assert.Equal(
            secondAggregateId,
            result[secondAggregateId][0].EventExecutionInfo.AggregateId
        );
    }

    [Fact]
    public async Task Write_PersistsEventsAndInvalidatesAffectedCacheEntries()
    {
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        await Seed(CreateEvent(aggregateId, 1));
        await _eventStore.GetEvents(aggregateId);
        Assert.True(_cache.TryGetValue(GetCacheKey(aggregateId), out _));

        var newEvent = CreateEvent(aggregateId, 2);
        await _eventStore.Write([ newEvent ]);

        Assert.False(_cache.TryGetValue(GetCacheKey(aggregateId), out _));
        Assert.Equal(2, await _dbContext.SerializedEventPayload.CountAsync());
    }

    [Fact]
    public async Task Write_PersistsUniqueConstraintsFromPayload()
    {
        var payload = CreateEvent(AggregateId.FromDatabaseGuid(Guid.NewGuid()), 1);
        payload
            .UniqueEventConstraintsToAdd
            .Add(new UniqueEventConstraintData("email", "user@example.com"));

        await _eventStore.Write([ payload ]);

        var constraint = await _dbContext.UniqueEventConstraints.SingleAsync();
        Assert.Equal(32, constraint.ConstraintHash.Length);
        Assert.Equal(payload.EventExecutionInfo.AggregateId.Value, constraint.AggregateId);
        Assert.Equal(payload.EventExecutionInfo.OrderNumber, constraint.OrderNumber);
        Assert.Equal("email", constraint.ConstraintName);
        Assert.Equal(payload.EventExecutionInfo.StateMachineId, constraint.StateMachineId);
    }

    [Fact]
    public async Task GetEvents_ReturnsEmptyArrayForAggregateWithoutEvents()
    {
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());

        var result = await _eventStore.GetEvents(aggregateId);

        Assert.Empty(result[aggregateId]);
        Assert.True(
            _cache.TryGetValue<EventPayload[]>(GetCacheKey(aggregateId), out var cachedEvents)
        );
        Assert.Empty(cachedEvents!);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _cache.Dispose();
    }

    private async Task Seed(params EventPayload[] payloads)
    {
        await _dbContext
            .SerializedEventPayload
            .AddRangeAsync(payloads.Select(SerializedEventPayload.FromPayload));
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private static EventPayload CreateEvent(AggregateId aggregateId, uint orderNumber)
    {
        var payload = EventPayload.Create(
            EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
            aggregateId,
            "cache-tests",
            new TestEvent { Value = (int)orderNumber }
        );
        payload.EventExecutionInfo.OrderNumber = orderNumber;
        return payload;
    }

    private static string GetCacheKey(AggregateId aggregateId) =>
        "inMemoryEventStore:" + aggregateId;

    private sealed class TestEvent : IEvent
    {
        public int Value { get; set; }

        public object Apply(object stateData, EventExecutionInfo eventExecutionInfo) => stateData;
    }
}
