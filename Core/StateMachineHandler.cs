using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Providers;
using EventSourcing.Shared.Exceptions;
using EventSourcing.Shared.Models;
using Shared.Interfaces;

namespace EventSourcing.Core;

public class StateMachineHandler(
    IEventStore _eventStore,
    IEventValidatorProvider _validatorProvider,
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
            var aggregateEventsToExecute = eventsToExecute.Where(
                x => x.EventExecutionInfo.AggregateId == aggregateId
            );
            var existingEventsByAggregate = existingEvents[aggregateId]
                .ToList()
                .OrderBy(x => x.EventExecutionInfo.OrderNumber);

            var stateInfo = await GenerateStateInfo(
                aggregateId,
                existingEventsByAggregate,
                aggregateEventsToExecute
            );

            stateInfoDictionary.Add(aggregateId, stateInfo);
        }

        await _eventStore.Write(eventsToExecute.ToArray());

        return stateInfoDictionary;
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
        Guid aggregateId,
        IEnumerable<EventPayload> existingEvents,
        IEnumerable<EventPayload> aggregateEventsToExecute
    )
    {
        _orderNumberHelper.AssignOrderNumbers(existingEvents, aggregateEventsToExecute);

        var events = existingEvents
            .Concat(aggregateEventsToExecute)
            .OrderBy(x => x.EventExecutionInfo.OrderNumber);

        return await Calculate(events);
    }

    public async Task<StateInfo> Calculate(IEnumerable<EventPayload> eventPayloads)
    {
        var firstEventData = eventPayloads.First();
        var stateMachineId = firstEventData.EventExecutionInfo.StateMachineId;

        var stateData = await _stateDataProvider.GetStateDataByStateMachine(stateMachineId);

        var stateInfo = StateInfo.Create(
            stateData,
            stateMachineId,
            firstEventData.EventExecutionInfo.AggregateId
        );

        foreach (var payload in eventPayloads)
        {
            var prerequisiteValidators = await _validatorProvider.GetPreEventStateValidators(
                payload
            );
            await Validate(stateData, prerequisiteValidators.Select(x => x as IEventValidator));
            stateData = payload.EventData.Apply(stateData, payload.EventExecutionInfo);
            stateInfo.StateData = stateData;
            stateInfo.CurrentOrderNumber = payload.EventExecutionInfo.OrderNumber;
            stateInfo.LastUpdateTimestamp = payload.EventExecutionInfo.Timestamp;
            var postrequisiteValidators = await _validatorProvider.GetPostEventStateValidators(
                payload
            );
            await Validate(stateData, postrequisiteValidators.Select(x => x as IEventValidator));
        }

        return stateInfo;
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
