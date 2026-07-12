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

    public async Task<Dictionary<Guid, EventPayload[]>> GetEvents(params Guid[] aggregateIds)
    {
        var distinctAggregateIds = aggregateIds.Distinct().ToArray();
        var cachedPayloads = GetPayloadsFromCache(distinctAggregateIds).ToArray();
        var missingPayloads = await GetMissingPayloads(cachedPayloads, distinctAggregateIds);

        var payloadsByAggregate = cachedPayloads
            .Concat(missingPayloads)
            .GroupBy(payload => payload.EventExecutionInfo.AggregateId)
            .ToDictionary(
                group => group.Key,
                group =>
                    group
                        .GroupBy(payload => payload.EventExecutionInfo.Id)
                        .Select(eventsWithSameId => eventsWithSameId.First())
                        .OrderBy(payload => payload.EventExecutionInfo.OrderNumber)
                        .ToArray()
            );

        var result = new Dictionary<Guid, EventPayload[]>(distinctAggregateIds.Length);

        foreach (var aggregateId in distinctAggregateIds)
        {
            var payloads = payloadsByAggregate.GetValueOrDefault(aggregateId) ?? [ ];
            cache.Set(GetCacheKey(aggregateId), payloads);
            result.Add(aggregateId, payloads);
        }

        return result;
    }

    protected virtual IEnumerable<EventPayload> GetPayloadsFromCache(params Guid[] aggregateIds) =>
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
                group => group.Key,
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

    public async Task Write(params EventPayload[] payloads)
    {
        await sqlEventStore.Write(payloads);

        // Force the next read to rebuild each affected aggregate from committed data.
        foreach (
            var aggregateId in payloads.Select(x => x.EventExecutionInfo.AggregateId).Distinct()
        )
            cache.Remove(GetCacheKey(aggregateId));
    }

    private static string GetCacheKey(Guid aggregateId) => CacheKeyPrefix + aggregateId;
}
