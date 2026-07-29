using EventSourcing.Persistence;
using EventSourcing.Persistence.Interfaces;
using EventSourcing.Persistence.Models;
using EventSourcing.Shared.Models;
using LinqKit;
using Microsoft.Extensions.Caching.Memory;

namespace EventSourcing.Optimizations;

public class EventStoreWithCache(BaseSqlEventStore sqlEventStore, IMemoryCache cache) : IEventStore
{
    private const string CacheKeyPrefix = "inMemoryEventStore:";

    public async Task<Dictionary<AggregateId, EventPayload[]>> GetEvents(
        params AggregateId[] aggregateIds
    )
    {
        var distinctAggregateIds = aggregateIds.Distinct().ToArray();
        var cachedPayloads = GetPayloadsFromCache(distinctAggregateIds).ToArray();
        var distinctAggregateGuids = distinctAggregateIds.Select(x => x.Value).ToArray();
        var missingPayloads = await GetMissingPayloads(cachedPayloads, distinctAggregateGuids);

        var payloadsByAggregate = cachedPayloads
            .Concat(missingPayloads)
            .DistinctBy(x => (x.EventExecutionInfo.AggregateId, x.EventExecutionInfo.OrderNumber))
            .GroupBy(payload => payload.EventExecutionInfo.AggregateId)
            .ToDictionary(
                group => group.First().EventExecutionInfo.AggregateId,
                group =>
                    group
                        .ToArray()
                        .OrderBy(payload => payload.EventExecutionInfo.OrderNumber)
                        .ToArray()
            );

        EventPayload[] payloads;
        foreach (var aggregateId in distinctAggregateIds)
        {
            if (!payloadsByAggregate.TryGetValue(aggregateId, out payloads!))
                payloadsByAggregate[aggregateId] = payloads =  [ ];

            cache.Set(GetCacheKey(aggregateId), payloads, TimeSpan.FromDays(1));
        }

        return payloadsByAggregate;
    }

    protected virtual IEnumerable<EventPayload> GetPayloadsFromCache(
        params AggregateId[] aggregateIds
    ) =>
        aggregateIds.SelectMany(
            aggregateId =>
                cache.TryGetValue(GetCacheKey(aggregateId), out EventPayload[]? payloads)
                    ? payloads ?? [ ]
                    : [ ]
        );

    protected virtual async Task<IEnumerable<EventPayload>> GetMissingPayloads(
        IEnumerable<EventPayload> cachedPayloads,
        params Guid[] aggregateIds
    )
    {
        var latestOrderNumbers = cachedPayloads
            .GroupBy(payload => payload.EventExecutionInfo.AggregateId)
            .ToDictionary(
                group => group.Key.Value,
                group => group.Max(payload => payload.EventExecutionInfo.OrderNumber)
            );

        var predicate = PredicateBuilder.New<SerializedEventPayload>(false);

        foreach (var aggregateId in aggregateIds)
        {
            var latestOrderNumber = latestOrderNumbers.GetValueOrDefault(aggregateId);
            predicate = predicate.Or(
                payload =>
                    payload.AggregateId == aggregateId && payload.OrderNumber > latestOrderNumber
            );
        }

        return await sqlEventStore.GetEvents(predicate);
    }

    public async Task Write(List<EventPayload> payloads) => await sqlEventStore.Write(payloads);

    private static string GetCacheKey(AggregateId aggregateId) => CacheKeyPrefix + aggregateId;
}
