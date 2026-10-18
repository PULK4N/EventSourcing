using EventSourcing.Shared.Models;

namespace EventSourcing.Optimizations;

public interface IStateInfoStore
{
    /// <summary>
    /// Returns dictionary of aggregates with their StateInfos.
    /// </summary>
    /// <remarks>
    /// If StateInfos are not found for an aggretate, the aggregate will not be present in the dictionary.
    /// </remarks>
    Task<Dictionary<AggregateId, StateInfo>> Get(List<AggregateId> aggregateIds);
    Task Write(List<StateInfo> stateInfos);
}
