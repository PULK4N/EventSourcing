using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Providers;
using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Exceptions;
using EventSourcing.Shared.Models;
using Shared.Interfaces;

namespace EventSourcing.Core;

public class StateMachineHandler(
    IEventStore _eventStore,
    IEventStoreWithOutbox _eventStoreWithOutbox,
    IEventValidatorProvider _validatorProvider,
    IUniqueEventConstraintProvider _uniqueEventConstraintProvider,
    IStateDataProvider _stateDataProvider,
    OrderNumberHelper _orderNumberHelper
)
{
    public async Task<Dictionary<AggregateId, StateInfo>> ExecuteEvents(
        List<EventPayload> eventsToExecute
    )
    {
        var aggregateIds = eventsToExecute
            .Select(x => x.EventExecutionInfo.AggregateId)
            .Distinct()
            .ToArray();
        var existingEvents = await _eventStore.GetEvents(aggregateIds);

        var stateInfoDictionary = new Dictionary<AggregateId, StateInfo>();

        foreach (var aggregateId in aggregateIds)
        {
            StateInfo stateInfo = await Calculate(
                eventsToExecute,
                existingEvents[aggregateId].ToList(),
                aggregateId
            );

            stateInfoDictionary.Add(aggregateId, stateInfo);
        }

        await _eventStoreWithOutbox.Write(eventsToExecute);

        return stateInfoDictionary;
    }

    private async Task<StateInfo> Calculate(
        List<EventPayload> eventsToExecute,
        List<EventPayload> existingEvents,
        AggregateId aggregateId
    )
    {
        eventsToExecute = eventsToExecute
            .Where(x => x.EventExecutionInfo.AggregateId == aggregateId)
            .ToList();
        existingEvents = existingEvents.OrderBy(x => x.EventExecutionInfo.OrderNumber).ToList();
        StateMachineEventValidator.ValidateSingleStateMachineForAggregate(
            aggregateId,
            eventsToExecute,
            existingEvents
        );

        _orderNumberHelper.AssignOrderNumbers(existingEvents, eventsToExecute);

        var allEvents = existingEvents
            .Concat(eventsToExecute)
            .OrderBy(x => x.EventExecutionInfo.OrderNumber)
            .ToList();
        var stateInfo = await Calculate(allEvents, eventsToExecute);
        return stateInfo;
    }

    public async Task<StateInfo> Calculate(
        List<EventPayload> eventPayloads,
        List<EventPayload> newPayloads
    )
    {
        var firstEventData = eventPayloads.First();
        var stateMachineId = firstEventData.EventExecutionInfo.StateMachineId;

        var stateData = await _stateDataProvider.GetStateDataByStateMachine(stateMachineId);

        var stateInfo = StateInfo.Create(
            stateData,
            stateMachineId,
            firstEventData.EventExecutionInfo.AggregateId
        );
        var newPayloadsSet = new HashSet<EventPayload>(newPayloads);

        foreach (var payload in eventPayloads)
        {
            var isNewEvent = newPayloadsSet.Contains(payload);
            stateData = await Calculate(stateData, stateInfo, payload, isNewEvent);
        }

        return stateInfo;
    }

    private async Task<object> Calculate(
        object stateData,
        StateInfo stateInfo,
        EventPayload payload,
        bool isNewEvent
    )
    {
        if (isNewEvent)
        {
            var prerequisiteValidators = await _validatorProvider.GetPreEventStateValidators(
                payload
            );
            Validate(stateData, payload, prerequisiteValidators);

            stateData = ApplyEventAndSetConstraints(stateData, payload);
        }
        else
        {
            stateData = payload.EventData.Apply(stateData, payload.EventExecutionInfo);
        }

        stateInfo.StateData = stateData;
        stateInfo.CurrentOrderNumber = payload.EventExecutionInfo.OrderNumber;
        stateInfo.LastUpdateTimestamp = payload.EventExecutionInfo.Timestamp;

        if (!isNewEvent)
            return stateData;

        var postrequisiteValidators = await _validatorProvider.GetPostEventStateValidators(payload);
        Validate(stateData, payload, postrequisiteValidators);
        return stateData;
    }

    private object ApplyEventAndSetConstraints(object stateData, EventPayload payload)
    {
        payload.UniqueEventConstraintsToRemove.Clear();
        payload
            .UniqueEventConstraintsToRemove
            .AddRange(_uniqueEventConstraintProvider.GetConstraintsToRemove(stateData, payload));
        stateData = payload.EventData.Apply(stateData, payload.EventExecutionInfo);

        payload.UniqueEventConstraintsToAdd.Clear();
        payload
            .UniqueEventConstraintsToAdd
            .AddRange(_uniqueEventConstraintProvider.GetConstraintsToAdd(stateData, payload));
        return stateData;
    }

    private void Validate(
        object stateData,
        EventPayload payload,
        IEnumerable<IEventValidator> validators
    )
    {
        var validationResults = validators
            .Select(validator => validator.Validate(stateData, payload))
            .ToList();

        var failedValidations = validationResults.Where(x => x.Succeded == false).ToList();

        if (!failedValidations.Any())
            return;

        throw new EventValidationException(failedValidations);
    }
}
