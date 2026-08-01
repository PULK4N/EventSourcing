using System.Linq.Expressions;
using EventSourcing.Persistence;
using EventSourcing.Persistence.Interfaces;
using EventSourcing.Persistence.Models;
using EventSourcing.Persistence.Serialization;
using EventSourcing.Shared.Helpers;
using EventSourcing.Shared.Models;
using LinqKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NRedisStack;
using StackExchange.Redis;

namespace EventSourcing.Optimizations
{
    public class EventStoreWithRedis : IEventStore
    {
        protected readonly IDatabase _database;
        protected readonly BaseSqlEventStore _sqlEventStore;
        protected TimeSpan ExpiryTime => TimeSpan.FromDays(1);

        public EventStoreWithRedis(BaseSqlEventStore sqlEventStore, IConfiguration configuration)
        {
            _sqlEventStore = sqlEventStore;

            var redisConnectionString =
                configuration["ConnectionStrings:Redis"]?.ToString() ?? "localhost";

            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(redisConnectionString);
            _database = redis.GetDatabase();
        }

        public async Task<Dictionary<AggregateId, List<EventPayload>>> GetEvents(
            List<AggregateId> aggregateIds
        )
        {
            var payloadsFromCache = await GetPayloadsFromCache(aggregateIds);

            var missingPayloads = await GetMissingPayloads(payloadsFromCache, aggregateIds);

            var payloads = missingPayloads.Concat(payloadsFromCache).ToList();

            var payloadsByAggregate = payloads.GetPayloadsByAggregateDictionary();

            foreach (var aggregateId in aggregateIds)
            {
                if (!payloadsByAggregate.TryGetValue(aggregateId, out payloads))
                    payloadsByAggregate[aggregateId] = payloads =  [ ];
                else
                {
                    var serializedPayloads = payloads.Select(SerializedEventPayload.FromPayload).ToList();
                    var payloadsSerialized = EventJsonSerializer.Serialize(
                        serializedPayloads
                    );
                    await _database.StringSetAsync(
                        GetRedisKey(aggregateId),
                        new RedisValue(payloadsSerialized),
                        ExpiryTime
                    );
                }
            }

            return payloadsByAggregate;
        }

        protected virtual async Task<IEnumerable<EventPayload>> GetPayloadsFromCache(
            List<AggregateId> aggregateIds
        )
        {
            var redisKeys = aggregateIds.Select(x => GetRedisKey(x)).ToArray();

            var cachedResults = await _database.StringGetAsync(redisKeys);

            var payloads = cachedResults
                .Where(x => x.HasValue)
                .SelectMany(
                    x => EventJsonSerializer.Deserialize<
                        List<SerializedEventPayload>
                    >(x.ToString())
                )
                .Select(x => x.Deserialize());

            return payloads;
        }

        protected virtual async Task<IEnumerable<EventPayload>> GetMissingPayloads(
            IEnumerable<EventPayload> cachedPayloads,
            List<AggregateId> aggregateIds
        )
        {
            var aggregateIdGuids = aggregateIds.Select(x => x.Value);
            var aggregateIdsWithOrderNumber = cachedPayloads
                .GroupBy(x => x.EventExecutionInfo.AggregateId)
                .Select(gr => gr.OrderByDescending(x => x.EventExecutionInfo.OrderNumber).First())
                .Select(
                    x =>
                        (
                            AggregateId: x.EventExecutionInfo.AggregateId.Value,
                            OrderNumber: x.EventExecutionInfo.OrderNumber
                        )
                )
                .ToList();

            AddMissingAggregateIds(aggregateIdsWithOrderNumber, aggregateIdGuids);

            var predicate = PredicateBuilder.New<SerializedEventPayload>(false);

            foreach (var aggregateWithOrderNumber in aggregateIdsWithOrderNumber)
            {
                predicate.Or(
                    x =>
                        x.AggregateId == aggregateWithOrderNumber.AggregateId
                        && x.OrderNumber > aggregateWithOrderNumber.OrderNumber
                );
            }
            Expression<Func<SerializedEventPayload, bool>> test = predicate;

            return await _sqlEventStore.GetEvents(predicate);
        }

        protected virtual void AddMissingAggregateIds(
            List<(Guid AggregateId, uint OrderNumber)> aggregateIdsWithOrderNumber,
            IEnumerable<Guid> aggregateIds
        )
        {
            var cachedAggregateIds = aggregateIdsWithOrderNumber.Select(x => x.AggregateId);

            var missingAggregateIds = aggregateIds.Where(x => !cachedAggregateIds.Contains(x));

            foreach (var aggregateId in missingAggregateIds)
            {
                aggregateIdsWithOrderNumber.Add((aggregateId, 0));
            }
        }

        public Task Write(List<EventPayload> payloads) => _sqlEventStore.Write(payloads);

        public RedisKey GetRedisKey(AggregateId aggregateId) =>
            new RedisKey("EventPayloadsByAggregateId:" + aggregateId.Value.ToString());
    }
}
