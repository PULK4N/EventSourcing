using EventSourcing.Core;
using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Models;
using Force.DeepCloner;
using Microsoft.Extensions.Caching.Memory;

namespace EventSourcing.Optimizations;

public class StateInfoStoreWithCache(
    IEventStore _eventStore,
    StateCalculator _stateCalculator,
    IMemoryCache _cache
) : IStateInfoStore
{
    private const string CacheKeyPrefix = "stateInfo:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromDays(1);

    public async Task<Dictionary<AggregateId, StateInfo>> Get(List<AggregateId> aggregateIds)
    {
        var stateInfos = new List<StateInfo>();
        var missingAggregateIds = new List<AggregateId>();

        foreach (var aggregateId in aggregateIds)
        {
            if (
                _cache.TryGetValue(GetCacheKey(aggregateId), out StateInfo? stateInfo)
                && stateInfo is not null
            )
                stateInfos.Add(DeepCopy(stateInfo));
            else
                missingAggregateIds.Add(aggregateId);
        }

        if (missingAggregateIds.Count == 0)
            return stateInfos.ToDictionary(x => x.AggregateId, x => x);

        var allPayloads = await _eventStore.GetEvents(missingAggregateIds);

        foreach (var (_, payloads) in allPayloads)
        {
            if (payloads.Any())
            {
                var stateInfo = _stateCalculator.Calculate(payloads, [ ]);
                stateInfos.Add(stateInfo);
                AddToCache(stateInfo);
            }
        }

        return stateInfos.ToDictionary(x => x.AggregateId, x => x);
    }

    public async Task Write(List<StateInfo> stateInfos)
    {
        foreach (var stateInfo in stateInfos)
        {
            AddToCache(stateInfo);
        }
    }

    private void AddToCache(StateInfo stateInfo) =>
        _cache.Set(GetCacheKey(stateInfo.AggregateId), DeepCopy(stateInfo), CacheDuration);

    /// <summary>
    /// Deep-copies the state without <see cref="StateInfo.LastExecutedPayloads"/>, which belong
    /// to the write that produced the state and not to the state itself.
    /// </summary>
    private static StateInfo DeepCopy(StateInfo stateInfo)
    {
        var copy = StateInfo.Create(
            stateInfo.StateData.DeepClone(),
            stateInfo.StateMachineId,
            stateInfo.AggregateId
        );
        copy.CurrentOrderNumber = stateInfo.CurrentOrderNumber;
        copy.LastUpdateTimestamp = stateInfo.LastUpdateTimestamp;
        copy.State = stateInfo.State;
        copy.UniqueConstraints = stateInfo.UniqueConstraints.DeepClone();

        return copy;
    }

    private static string GetCacheKey(AggregateId aggregateId) => CacheKeyPrefix + aggregateId;
}
