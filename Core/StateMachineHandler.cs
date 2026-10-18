using System.Threading.Tasks;
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

        return _stateCalculator.Calculate(payloads[aggregateId], [ ]);
    }

    public async Task<StateInfo> ExecuteEvents(EventPayload eventToExecute)
    {
        var executedEvents = await ExecuteEvents([ eventToExecute ]);
        return executedEvents[eventToExecute.EventExecutionInfo.AggregateId];
    }

    public Task<Dictionary<AggregateId, StateInfo>> ExecuteEvents(
        EventPayload conditionalEvent,
        Func<StateInfo[], List<EventPayload>> conditionalEventsMethod
    )
    {
        return ExecuteEvents(new List<EventPayload> { conditionalEvent }, conditionalEventsMethod);
    }

    /// <remarks>
    /// In the case we ever want to implement a retry - in the case of an exception, reset conditional event orderNumbers
    /// </remarks>
    public async Task<Dictionary<AggregateId, StateInfo>> ExecuteEvents(
        List<EventPayload> conditionalEvents,
        params Func<StateInfo[], List<EventPayload>>[] conditionalEventsMethods
    )
    {
        var conditionalEventAggregateIds = conditionalEvents
            .Select(p => p.EventExecutionInfo.AggregateId)
            .Distinct();

        var existingEvents = await _eventStoreWithOutbox.GetEvents(
            [ .. conditionalEventAggregateIds ]
        );

        var newEvents = new List<EventPayload>(conditionalEvents);

        var stateInfoDictionary = new Dictionary<AggregateId, StateInfo>();
        foreach (var conditionalEventsMethod in conditionalEventsMethods)
        {
            stateInfoDictionary = await ExecuteEventsInternal(existingEvents, newEvents);
            var generatedEvents = conditionalEventsMethod(stateInfoDictionary.Values.ToArray());
            await SetExistingEventsForNewAggregates(existingEvents, generatedEvents);

            newEvents.AddRange(generatedEvents);
            ResetOrderNumbers(newEvents);
        }

        stateInfoDictionary = await ExecuteEventsInternal(existingEvents, newEvents);

        await _eventStoreWithOutbox.Write(stateInfoDictionary);

        return stateInfoDictionary;
    }

    private void ResetOrderNumbers(List<EventPayload> conditionalEvents)
    {
        foreach (var eventPayload in conditionalEvents)
        {
            eventPayload.EventExecutionInfo.OrderNumber = EventExecutionInfo.UNSET_ORDER_NUMBER;
        }
    }

    private async Task SetExistingEventsForNewAggregates(
        Dictionary<AggregateId, List<EventPayload>> existingEvents,
        List<EventPayload> generatedEvents
    )
    {
        var newAggregates = generatedEvents
            .Select(x => x.EventExecutionInfo.AggregateId)
            .Except(existingEvents.Keys)
            .ToList();

        if (!newAggregates.Any())
            return;

        var newEvents = await _eventStoreWithOutbox.GetEvents(newAggregates);

        foreach (var aggregateId in newAggregates)
        {
            if (newEvents.TryGetValue(aggregateId, out var existingEventsForNewAggregate))
                existingEvents[aggregateId] = existingEventsForNewAggregate;
        }
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

    protected Task<Dictionary<AggregateId, StateInfo>> ExecuteEventsInternal(
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

            var stateInfo = _stateCalculator.Calculate(
                previousEvents,
                eventsToExecuteByAggregate[aggregateId]
            );

            stateInfoDictionary.Add(aggregateId, stateInfo);
        }

        return Task.FromResult(stateInfoDictionary);
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
