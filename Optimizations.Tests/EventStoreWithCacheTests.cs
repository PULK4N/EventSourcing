using System.Text.Json;
using EventSourcing.Persistence;
using EventSourcing.Persistence.Models;
using EventSourcing.Shared.Containers;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace EventSourcing.Optimizations.Tests;

public class EventStoreWithCacheTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly EventSourcingDbContext _dbContext;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly EventStoreWithCache _eventStore;

    static EventStoreWithCacheTests()
    {
        EventTypeContainer.AddEventType(typeof(TestEvent));
    }

    public EventStoreWithCacheTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<EventSourcingDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new EventSourcingDbContext(options);
        _dbContext.Database.EnsureCreated();
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
        var result = await _eventStore.GetEvents([ aggregateId ]);
        var resultValues = result.SelectMany(x => x.Value).ToList();
        Assert.Equal(events.Select(Describe), resultValues.Select(Describe));

        Assert.True(
            _cache.TryGetValue<List<EventPayload>>(GetCacheKey(aggregateId), out var cachedEvents)
        );
        Assert.Equal(2, cachedEvents!.Count);
    }

    [Fact]
    public async Task GetEvents_AppendsEventsAddedAfterAggregateWasCached()
    {
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var firstEvent = CreateEvent(aggregateId, 1);
        await Seed(firstEvent);
        await _eventStore.GetEvents([ aggregateId ]);

        var secondEvent = CreateEvent(aggregateId, 2);
        await Seed(secondEvent);

        var result = await _eventStore.GetEvents([ aggregateId ]);

        Assert.Equal(
            [ 1u, 2u ],
            result[aggregateId].Select(payload => payload.EventExecutionInfo.OrderNumber)
        );
        Assert.True(
            _cache.TryGetValue<List<EventPayload>>(GetCacheKey(aggregateId), out var cachedEvents)
        );
        Assert.Equal(2, cachedEvents!.Count);
    }

    [Fact]
    public async Task GetEvents_ReturnsEventsForEachDistinctAggregate()
    {
        var firstAggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var secondAggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        await Seed(CreateEvent(firstAggregateId, 1), CreateEvent(secondAggregateId, 1));

        var result = await _eventStore.GetEvents(
            [ firstAggregateId, secondAggregateId, firstAggregateId ]
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
    public async Task Write_PersistsEventsAndRefreshesCacheOnNextRead()
    {
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        await Seed(CreateEvent(aggregateId, 1));
        await _eventStore.GetEvents([ aggregateId ]);
        Assert.True(_cache.TryGetValue(GetCacheKey(aggregateId), out _));

        var newEvent = CreateEvent(aggregateId, 2);
        var stateInfo = StateInfo.Create(new object(), "cache-tests", aggregateId);
        stateInfo.LastExecutedPayloads = [ newEvent ];
        await _eventStore.Write([ stateInfo ]);

        Assert.Equal(2, await _dbContext.SerializedEventPayload.CountAsync());

        var result = await _eventStore.GetEvents([ aggregateId ]);

        Assert.Equal(
            [ 1u, 2u ],
            result[aggregateId].Select(payload => payload.EventExecutionInfo.OrderNumber)
        );
        Assert.True(
            _cache.TryGetValue<List<EventPayload>>(GetCacheKey(aggregateId), out var cachedEvents)
        );
        Assert.Equal(2, cachedEvents!.Count);
    }

    [Fact]
    public async Task GetEvents_ReturnsEmptyListForAggregateWithoutEvents()
    {
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());

        var result = await _eventStore.GetEvents([ aggregateId ]);

        Assert.Empty(result[aggregateId]);
        Assert.True(
            _cache.TryGetValue<List<EventPayload>>(GetCacheKey(aggregateId), out var cachedEvents)
        );
        Assert.Empty(cachedEvents!);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
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

    /// <remarks>
    /// SQLite does not keep <see cref="DateTimeKind"/>, so the timestamp is compared by ticks.
    /// </remarks>
    private static string Describe(EventPayload payload) =>
        JsonSerializer.Serialize(
            new
            {
                payload.EventExecutionInfo.AggregateId,
                payload.EventExecutionInfo.OrderNumber,
                payload.EventExecutionInfo.EventName,
                payload.EventExecutionInfo.StateMachineId,
                payload.EventExecutionInfo.EventExecutor,
                Timestamp = payload.EventExecutionInfo.Timestamp.Ticks,
                EventData = (object)payload.EventData
            }
        );

    private static string GetCacheKey(AggregateId aggregateId) =>
        "inMemoryEventStore:" + aggregateId;

    private sealed class TestEvent : IEvent
    {
        public int Value { get; set; }

        public object Apply(object stateData, EventExecutionInfo eventExecutionInfo) => stateData;
    }
}
