using EventSourcing.Persistence;
using EventSourcing.Persistence.Interfaces;
using EventSourcing.Persistence.Models;
using EventSourcing.Shared.Helpers;
using EventSourcing.Shared.Models;
using LinqKit;
using Microsoft.Extensions.Caching.Memory;

namespace EventSourcing.Optimizations;

public class EventStoreWithCache(BaseSqlEventStore sqlEventStore, IMemoryCache cache) : IEventStore
{
    private const string CacheKeyPrefix = "inMemoryEventStore:";

    public async Task<Dictionary<AggregateId, List<EventPayload>>> GetEvents(
        List<AggregateId> aggregateIds
    )
    {
        var distinctAggregateIds = aggregateIds.Distinct().ToList();
        var cachedPayloads = GetPayloadsFromCache(distinctAggregateIds).ToList();
        var distinctAggregateGuids = distinctAggregateIds.Select(x => x.Value).ToList();
        var missingPayloads = await GetMissingPayloads(cachedPayloads, distinctAggregateGuids);

        var payloadsByAggregate = cachedPayloads
            .Concat(missingPayloads)
            .ToList()
            .GetPayloadsByAggregateDictionary();

        var payloads = new List<EventPayload>();
        foreach (var aggregateId in distinctAggregateIds)
        {
            if (!payloadsByAggregate.TryGetValue(aggregateId, out payloads))
                payloadsByAggregate[aggregateId] = payloads =  [ ];

            cache.Set(GetCacheKey(aggregateId), payloads, TimeSpan.FromDays(1));
        }

        return payloadsByAggregate;
    }

    protected virtual List<EventPayload> GetPayloadsFromCache(List<AggregateId> aggregateIds) =>
        aggregateIds
            .SelectMany(
                aggregateId =>
                    cache.TryGetValue(GetCacheKey(aggregateId), out List<EventPayload>? payloads)
                        ? payloads ?? [ ]
                        : [ ]
            )
            .ToList();

    protected virtual async Task<IEnumerable<EventPayload>> GetMissingPayloads(
        List<EventPayload> cachedPayloads,
        List<Guid> aggregateIds
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
