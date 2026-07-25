using System.Linq.Expressions;
using EventSourcing.Persistence;
using EventSourcing.Persistence.Interfaces;
using EventSourcing.Persistence.Models;
using EventSourcing.Shared.Models;
using LinqKit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using NRedisStack;
using StackExchange.Redis;

namespace EventSourcing.Optimizations
{
    public class EventStoreWithRedis : IEventStore
    {
        protected readonly IDatabase _database;
        protected readonly BaseSqlEventStore _sqlEventStore;

        public EventStoreWithRedis(BaseSqlEventStore sqlEventStore, IConfiguration configuration)
        {
            _sqlEventStore = sqlEventStore;

            var redisConnectionString =
                configuration["ConnectionStrings:Redis"]?.ToString() ?? "localhost";

            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(redisConnectionString);
            _database = redis.GetDatabase();
        }

        public async Task<Dictionary<AggregateId, EventPayload[]>> GetEvents(
            params AggregateId[] aggregateIds
        )
        {
            var payloadsFromCache = await GetPayloadsFromCache(aggregateIds);

            var aggregateIdGuids = aggregateIds.Select(x => x.Value).ToArray();
            var missingPayloads = await GetMissingPayloads(payloadsFromCache, aggregateIdGuids);

            var payloads = missingPayloads.Concat(payloadsFromCache);

            var eventsDictionary = new Dictionary<AggregateId, EventPayload[]>();

            foreach (var aggregateId in aggregateIds)
            {
                var aggregateEvents = payloads
                    .Where(x => x.EventExecutionInfo.AggregateId == aggregateId)
                    .ToArray();
                eventsDictionary.Add(aggregateId, aggregateEvents);
            }

            return eventsDictionary;
        }

        protected virtual async Task<IEnumerable<EventPayload>> GetPayloadsFromCache(
            params AggregateId[] aggregateIds
        )
        {
            var redisKeys = aggregateIds
                .Select(x => new RedisKey("redisEventStore:" + x.ToString()))
                .ToArray();

            var cachedResults = await _database.StringGetAsync(redisKeys);

            var cachedResultsDeserialized = cachedResults
                .Where(x => x.HasValue)
                .Select(x => JsonConvert.DeserializeObject<SerializedEventPayload>(x.ToString()));

            var payloads = cachedResultsDeserialized.Select(x => x!.Deserialize());

            return payloads;
        }

        protected virtual async Task<IEnumerable<EventPayload>> GetMissingPayloads(
            IEnumerable<EventPayload> cachedPayloads,
            params Guid[] aggregateIds
        )
        {
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

            AddMissingAggregateIds(aggregateIdsWithOrderNumber, aggregateIds);

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

        public Task Write(params EventPayload[] payloads) => _sqlEventStore.Write(payloads);
    }
}
