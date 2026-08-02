using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Helpers;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core;

public class StateMachineHandler(
    StateCalculator _stateCalculator,
    IEventStoreWithOutbox _eventStoreWithOutbox
)
{
    // TODO: move elsewhere
    public async Task<StateInfo?> GetByAggregateId(AggregateId aggregateId)
    {
        var payloads = await _eventStoreWithOutbox.GetEvents([ aggregateId ]);
        if (!payloads.ContainsKey(aggregateId) || payloads[aggregateId].Count == 0)
            return null;

        return await _stateCalculator.Calculate(payloads[aggregateId], [ ]);
    }

    public async Task<StateInfo> ExecuteEvents(EventPayload eventToExecute) =>
        (await ExecuteEvents([ eventToExecute ]))[eventToExecute.EventExecutionInfo.AggregateId];

    /// <summary>
    /// Generates stateInfo by executing conditional event
    /// Then based on those stataInfo it generates new events by delegate
    /// Then executes again conditional event + new generated events together.
    /// </summary>
    /// <param name="conditionalEvent"></param>
    /// <param name="conditionalEventsMethod"></param>
    /// <returns>AggregateId -> StateInfo dictionary of executed conditional event + generated events</returns>
    public async Task<Dictionary<AggregateId, StateInfo>> ExecuteEvents(
        EventPayload conditionalEvent,
        Func<StateInfo, List<EventPayload>> conditionalEventsMethod
    )
    {
        var conditionalEventAggregateId = conditionalEvent.EventExecutionInfo.AggregateId;
        var existingEvents = await _eventStoreWithOutbox.GetEvents([ conditionalEventAggregateId ]);

        var stateInfoDictionary = await ExecuteEventsInternal(existingEvents, [ conditionalEvent ]);
        var conditionalEventStateInfo = stateInfoDictionary[conditionalEventAggregateId];

        var generatedEvents = conditionalEventsMethod(
            stateInfoDictionary[conditionalEventAggregateId]
        );

        var aggregateIds = generatedEvents
            .Select(x => x.EventExecutionInfo.AggregateId)
            .Distinct()
            .ToList();

        existingEvents = await _eventStoreWithOutbox.GetEvents(aggregateIds);

        var conditionalAggregateEvents = existingEvents.ContainsKey(conditionalEventAggregateId)
            ? existingEvents[conditionalEventAggregateId].Append(conditionalEvent).ToList()
            : [ conditionalEvent ];
        existingEvents[conditionalEventAggregateId] = conditionalAggregateEvents;

        StateValidator.ValidateOldEventsContainDuplicateOrderNumbers(conditionalAggregateEvents);

        stateInfoDictionary = await ExecuteEventsInternal(existingEvents, generatedEvents);
        AddConditionalEventToStateInfo(stateInfoDictionary, conditionalEventStateInfo);

        await _eventStoreWithOutbox.Write(stateInfoDictionary);

        return stateInfoDictionary;
    }

    public async Task<Dictionary<AggregateId, StateInfo>> ExecuteEvents(
        List<EventPayload> eventsToExecute
    )
    {
        var aggregateIds = eventsToExecute
            .Select(x => x.EventExecutionInfo.AggregateId)
            .Distinct()
            .ToList();

        var existingEvents = await _eventStoreWithOutbox.GetEvents(aggregateIds);

        var stateInfoDictionary = await ExecuteEventsInternal(existingEvents, eventsToExecute);

        await _eventStoreWithOutbox.Write(stateInfoDictionary);

        return stateInfoDictionary;
    }

    protected async Task<Dictionary<AggregateId, StateInfo>> ExecuteEventsInternal(
        Dictionary<AggregateId, List<EventPayload>> existingEvents,
        List<EventPayload> eventsToExecute
    )
    {
        var aggregateIds = eventsToExecute
            .Select(x => x.EventExecutionInfo.AggregateId)
            .Distinct()
            .ToList();

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

        return stateInfoDictionary;
    }

    private void AddConditionalEventToStateInfo(
        Dictionary<AggregateId, StateInfo> stateInfoDictionary,
        StateInfo conditionalEventStateInfo
    )
    {
        if (stateInfoDictionary.ContainsKey(conditionalEventStateInfo.AggregateId))
            stateInfoDictionary[conditionalEventStateInfo.AggregateId].LastExecutedPayloads =
                stateInfoDictionary[conditionalEventStateInfo.AggregateId]
                    .LastExecutedPayloads
                    .Prepend(conditionalEventStateInfo.LastExecutedPayloads[0])
                    .ToList();
        else
            stateInfoDictionary[conditionalEventStateInfo.AggregateId] = conditionalEventStateInfo;
    }
}
