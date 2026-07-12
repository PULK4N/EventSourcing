using EventSourcing.Core.Interfaces;
using EventSourcing.Shared.Containers;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Providers;

public sealed class StateMachineUniqueEventConstraintProvider(
    IStateMachineDefinitionProvider stateMachineDefinitions
) : IUniqueEventConstraintProvider
{
    public IEnumerable<UniqueEventConstraintData> GetConstraintsToAdd(
        object stateData,
        EventPayload payload
    ) =>
        GetConstraintCreators(payload).SelectMany(creator =>
            creator.CreateConstraintsToAdd(stateData, payload)
        );

    public IEnumerable<UniqueEventConstraintData> GetConstraintsToRemove(
        object stateData,
        EventPayload payload
    ) =>
        GetConstraintCreators(payload).SelectMany(creator =>
            creator.CreateConstraintsToRemove(stateData, payload)
        );

    private IEnumerable<IUniqueConstraintCreator> GetConstraintCreators(EventPayload payload)
    {
        var definition = stateMachineDefinitions.Get(
            payload.EventExecutionInfo.StateMachineId
        );

        if (!definition.Events.TryGetValue(
                payload.EventExecutionInfo.EventName,
                out var eventDefinition
            ))
            return [];

        return eventDefinition.UniqueConstraints.Select(
            ConstraintCreatorTypeContainer.GetUniqueEventConstraintCreator
        );
    }
}
