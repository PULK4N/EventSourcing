using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Providers;
using EventSourcing.Shared.Containers;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core;

public class StateCalculator(
    OrderNumberHelper _orderNumberHelper,
    IStateDataProvider _stateDataProvider,
    IEventValidatorProvider _validatorProvider,
    IStateMachineDefinitionProvider _stateMachineDefinitionsProvider
)
{
    public async Task<StateInfo> Calculate(
        List<EventPayload> existingPayloads,
        List<EventPayload> newPayloads
    )
    {
        var stateMachineId =
            existingPayloads.Count > 0
                ? existingPayloads.First().EventExecutionInfo.StateMachineId
                : newPayloads.First().EventExecutionInfo.StateMachineId;

        var stateMachineDefinition = _stateMachineDefinitionsProvider.Get(stateMachineId);

        StateValidator.ValidateOldEventsContainOrderNumbers(existingPayloads);
        StateValidator.ValidateNewEventsOrderNumbers(newPayloads);
        StateValidator.ValidateInitializationEvents(
            existingPayloads,
            newPayloads,
            stateMachineDefinition.InitializationEvents
        );

        existingPayloads = existingPayloads.OrderBy(x => x.EventExecutionInfo.OrderNumber).ToList();

        var allPayloads = existingPayloads.Concat(newPayloads).ToList();
        var firstPayload = allPayloads.First();

        StateValidator.ValidateAllEventsHaveSameAggregateIdAndStateMachineId(
            allPayloads,
            firstPayload
        );

        var stateInfo = await GetInitialStateInfo(firstPayload);

        _orderNumberHelper.AssignOrderNumbers(existingPayloads, newPayloads);

        foreach (var payload in existingPayloads)
        {
            Apply(stateInfo, payload);
        }

        foreach (var payload in newPayloads)
        {
            var prerequisiteValidators = await _validatorProvider.GetPreEventStateValidators(
                payload
            );
            StateValidator.ValidateEvent(stateInfo.StateData, payload, prerequisiteValidators);

            Apply(stateInfo, payload);

            var postrequisiteValidators = await _validatorProvider.GetPostEventStateValidators(
                payload
            );
            StateValidator.ValidateEvent(stateInfo.StateData, payload, postrequisiteValidators);
        }

        stateInfo.LastExecutedPayloads = newPayloads;
        stateInfo.UniqueConstraints = CreateUniqueConstraints(stateInfo);

        return stateInfo;
    }

    private static List<UniqueConstraint> CreateUniqueConstraints(StateInfo stateInfo)
    {
        if (stateInfo.StateData is ISharedStateData { IsDeleted: true })
            return [ ];

        return UniqueConstraintCreatorContainer
            .GetUniqueConstraintCreators(stateInfo.StateData.GetType())
            .SelectMany(creator => creator.Create(stateInfo.StateData))
            .ToList();
    }

    private async Task<StateInfo> GetInitialStateInfo(EventPayload firstPayload)
    {
        var stateMachineId = firstPayload.EventExecutionInfo.StateMachineId;
        var stateData = await _stateDataProvider.GetStateDataByStateMachine(
            stateMachineId,
            firstPayload.EventExecutionInfo.AggregateId
        );

        var stateInfo = StateInfo.Create(
            stateData,
            stateMachineId,
            firstPayload.EventExecutionInfo.AggregateId
        );
        return stateInfo;
    }

    private void Apply(StateInfo stateInfo, EventPayload payload)
    {
        stateInfo.StateData = payload
            .EventData
            .Apply(stateInfo.StateData, payload.EventExecutionInfo);

        stateInfo.CurrentOrderNumber = payload.EventExecutionInfo.OrderNumber;
        stateInfo.LastUpdateTimestamp = payload.EventExecutionInfo.Timestamp;
    }
}
