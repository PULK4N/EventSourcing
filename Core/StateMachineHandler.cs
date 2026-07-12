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
    public async Task<Dictionary<Guid, StateInfo>> ExecuteEvents(
        params EventPayload[] eventsToExecute
    )
    {
        var aggregateIds = eventsToExecute
            .Select(x => x.EventExecutionInfo.AggregateId)
            .Distinct()
            .ToArray();
        var existingEvents = await _eventStore.GetEvents(aggregateIds);

        var stateInfoDictionary = new Dictionary<Guid, StateInfo>();

        foreach (var aggregateId in aggregateIds)
        {
            var aggregateEventsToExecute = eventsToExecute
                .Where(x => x.EventExecutionInfo.AggregateId == aggregateId)
                .ToList();
            var existingEventsByAggregate = existingEvents[aggregateId]
                .OrderBy(x => x.EventExecutionInfo.OrderNumber)
                .ToList();
            ValidateSingleStateMachine(
                aggregateId,
                aggregateEventsToExecute,
                existingEventsByAggregate
            );

            var stateInfo = await GenerateStateInfo(
                existingEventsByAggregate,
                aggregateEventsToExecute
            );

            stateInfoDictionary.Add(aggregateId, stateInfo);
        }

        await _eventStoreWithOutbox.Write(eventsToExecute);

        return stateInfoDictionary;
    }

    private static void ValidateSingleStateMachine(
        Guid aggregateId,
        List<EventPayload> aggregateEventsToExecute,
        List<EventPayload> existingEventsByAggregate
    )
    {
        var stateMachineIds = aggregateEventsToExecute
            .Concat(existingEventsByAggregate)
            .Select(x => x.EventExecutionInfo.StateMachineId)
            .ToHashSet();

        if (stateMachineIds.Count > 1)
            throw new InvalidOperationException(
                $"Events for aggregate '{aggregateId}' belong to multiple state machines: "
                    + string.Join(", ", stateMachineIds)
            );
    }

    // TODO: think how to implement impersonate
    // Idea: Store impersonate data in some cache in a separate module
    // Core modules -> ImpersonateModule
    // Executor Module -> ImpersonateModule
    // Executing action handles if Impersonating is On for a user
    // Stores it in ImpersonateModule
    // But since we have the same executor, how do we know that he executed it?
    // We can since it's from the SAME SCOPE, and impersonating instance is registered as SCOPED.
    public async Task<StateInfo> GenerateStateInfo(
        List<EventPayload> existingEvents,
        List<EventPayload> aggregateEventsToExecute
    )
    {
        _orderNumberHelper.AssignOrderNumbers(existingEvents, aggregateEventsToExecute);

        var allEvents = existingEvents
            .Concat(aggregateEventsToExecute)
            .OrderBy(x => x.EventExecutionInfo.OrderNumber)
            .ToList();

        return await Calculate(allEvents, aggregateEventsToExecute);
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
            stateData = await Calculate(stateData, stateInfo, payload, newPayloadsSet);
        }

        return stateInfo;
    }

    private async Task<object> Calculate(
        object stateData,
        StateInfo stateInfo,
        EventPayload payload,
        HashSet<EventPayload> newPayloads
    )
    {
        if (newPayloads.Contains(payload))
        {
            var prerequisiteValidators = await _validatorProvider.GetPreEventStateValidators(
                payload
            );
            await Validate(stateData, prerequisiteValidators.Select(x => x as IEventValidator));

            stateData = ApplyEventAndSetConstraints(stateData, payload);
        }
        else
        {
            stateData = payload.EventData.Apply(stateData, payload.EventExecutionInfo);
        }

        stateInfo.StateData = stateData;
        stateInfo.CurrentOrderNumber = payload.EventExecutionInfo.OrderNumber;
        stateInfo.LastUpdateTimestamp = payload.EventExecutionInfo.Timestamp;

        if (newPayloads.Contains(payload))
        {
            var postrequisiteValidators = await _validatorProvider.GetPostEventStateValidators(
                payload
            );
            await Validate(stateData, postrequisiteValidators.Select(x => x as IEventValidator));
        }
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

    private async Task Validate(object stateData, IEnumerable<IEventValidator> validators)
    {
        var tasks = validators.Select(v => v.Validate(stateData)).ToArray();
        var validationResults = await Task.WhenAll(tasks);

        var failedValidations = validationResults.Where(x => x.Succeded == false).ToList();

        if (!failedValidations.Any())
            return;

        throw new EventValidationException(failedValidations);
    }
}
