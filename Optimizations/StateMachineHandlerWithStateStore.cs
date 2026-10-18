using EventSourcing.Core;
using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Models;

namespace EventSourcing.Optimizations;

/// <remarks>
/// Not meant to be used if app is meant to scale horisontally, since stateInfos can become stale.
/// </remarks>
public class StateMachineHandlerWithStateStore(
    StateCalculator _stateCalculator,
    IStateInfoStore _stateInfoStore,
    IEventStoreWithOutbox _eventStoreWithOutbox
) : StateMachineHandler(_stateCalculator, _eventStoreWithOutbox)
{
    public override async Task<StateInfo?> GetByAggregateId(AggregateId aggregateId)
    {
        var stateInfos = await _stateInfoStore.Get([ aggregateId ]);
        if (!stateInfos.TryGetValue(aggregateId, out var stateInfo))
            return null;

        return stateInfo;
    }

    public override async Task<Dictionary<AggregateId, StateInfo>> ExecuteEvents(
        List<EventPayload> conditionalEvents,
        params Func<StateInfo[], List<EventPayload>>[] conditionalEventsMethods
    )
    {
        var stateInfos = await base.ExecuteEvents(conditionalEvents, conditionalEventsMethods);
        await _stateInfoStore.Write([ .. stateInfos.Values ]);
        return stateInfos;
    }
}
