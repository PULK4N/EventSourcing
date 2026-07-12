using EventSourcing.Core.Interfaces;
using EventSourcing.Shared.Containers;
using EventSourcing.Shared.Exceptions;

namespace EventSourcing.Core.Providers;

public sealed class StateMachineStateDataProvider(
    IStateMachineDefinitionProvider stateMachineDefinitions
) : IStateDataProvider
{
    public Task<object> GetStateDataByStateMachine(string stateMachineId)
    {
        var definition = stateMachineDefinitions.Get(stateMachineId);
        var stateDataType = StateDataTypeContainer.GetStateDataType(definition.StateData);
        var stateData = Activator.CreateInstance(stateDataType);

        if (stateData is null)
            throw new StateDataNotRegisteredException(definition.StateData);

        return Task.FromResult(stateData);
    }
}
