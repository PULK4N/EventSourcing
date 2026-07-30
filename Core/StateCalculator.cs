using System.Text.Json;
using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Providers;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core;

public class StateCalculator(
    OrderNumberHelper _orderNumberHelper,
    IStateDataProvider _stateDataProvider,
    IEventValidatorProvider _validatorProvider,
    IUniqueEventConstraintProvider _uniqueEventConstraintProvider
)
{
    public async Task<StateInfo> Calculate(
        List<EventPayload> existingPayloads,
        List<EventPayload> newPayloads
    )
    {
        StateValidator.ValidateOldEventsContainOrderNumbers(existingPayloads);
        StateValidator.ValidateNewEventsOrderNumbers(newPayloads);

        existingPayloads = existingPayloads.OrderBy(x => x.EventExecutionInfo.OrderNumber).ToList();

        var allPayloads = existingPayloads.Concat(newPayloads).ToList();
        var firstPayload = allPayloads.First();

        if (
            !allPayloads.All(
                x =>
                    x.EventExecutionInfo.AggregateId == firstPayload.EventExecutionInfo.AggregateId
                    && x.EventExecutionInfo.StateMachineId
                        == firstPayload.EventExecutionInfo.StateMachineId
            )
        )
            throw new InvalidOperationException(
                $"Provided events must contain same aggregate id and state machine id! SerializedPayloads {JsonSerializer.Serialize(allPayloads)}"
            );

        var stateMachineId = firstPayload.EventExecutionInfo.StateMachineId;
        var stateData = await _stateDataProvider.GetStateDataByStateMachine(stateMachineId);

        var stateInfo = StateInfo.Create(
            stateData,
            stateMachineId,
            firstPayload.EventExecutionInfo.AggregateId
        );
        var newPayloadsSet = new HashSet<EventPayload>(newPayloads);
        _orderNumberHelper.AssignOrderNumbers(existingPayloads, newPayloads);

        foreach (var payload in allPayloads)
        {
            var isNewEvent = newPayloadsSet.Contains(payload);
            stateInfo.StateData = await Calculate(
                stateInfo.StateData,
                stateInfo,
                payload,
                isNewEvent
            );
            stateInfo.CurrentOrderNumber = payload.EventExecutionInfo.OrderNumber;
            stateInfo.LastUpdateTimestamp = payload.EventExecutionInfo.Timestamp;
        }
        stateInfo.LastExecutedPayloads = newPayloads;

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
            StateValidator.ValidateEvent(stateData, payload, prerequisiteValidators);

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
        StateValidator.ValidateEvent(stateData, payload, postrequisiteValidators);
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
}
