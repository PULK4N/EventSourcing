using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Helpers;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core;

public class StateMachineHandler(
    StateCalculator _stateCalculator,
    IEventStore _eventStore,
    IEventStoreWithOutbox _eventStoreWithOutbox
)
{
    public async Task<Dictionary<AggregateId, StateInfo>> ExecuteEvents(
        EventPayload eventToExecute
    ) => await ExecuteEvents([ eventToExecute ]);

    public async Task<Dictionary<AggregateId, StateInfo>> ExecuteEvents(
        List<EventPayload> eventsToExecute
    )
    {
        var aggregateIds = eventsToExecute
            .Select(x => x.EventExecutionInfo.AggregateId)
            .Distinct()
            .ToList();
        var existingEvents = await _eventStore.GetEvents(aggregateIds);

        var stateInfoDictionary = new Dictionary<AggregateId, StateInfo>();

        var eventsToExecuteByAggregate = eventsToExecute.GetPayloadsByAggregateDictionary();
        foreach (var aggregateId in aggregateIds)
        {
            var previousEvents = existingEvents.ContainsKey(aggregateId)
                ? existingEvents[aggregateId]
                : [ ];

            var stateInfo = await _stateCalculator.Calculate(
                previousEvents,
                eventsToExecuteByAggregate[aggregateId]
            );

            stateInfoDictionary.Add(aggregateId, stateInfo);
        }

        await _eventStoreWithOutbox.Write(stateInfoDictionary);

        return stateInfoDictionary;
    }
}
