using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Models;
using EventSourcing.Shared.Containers;
using EventSourcing.Shared.Models;
using Shared.Interfaces;

namespace EventSourcing.Core.Providers;

public sealed class EventValidatorProvider(
    IStateMachineDefinitionProvider stateMachineDefinitions
) : IEventValidatorProvider
{
    public Task<List<IPostEventValidator>> GetPostEventStateValidators(
        EventPayload payload
    ) =>
        Task.FromResult(
            GetValidators<IPostEventValidator>(
                payload,
                eventDefinition => eventDefinition.PostEventValidators
            )
        );

    public Task<List<IPreEventValidator>> GetPreEventStateValidators(
        EventPayload payload
    ) =>
        Task.FromResult(
            GetValidators<IPreEventValidator>(
                payload,
                eventDefinition => eventDefinition.PreEventValidators
            )
        );

    private List<TValidator> GetValidators<TValidator>(
        EventPayload payload,
        Func<StateMachineEventDefinition, IEnumerable<string>> getValidatorNames
    )
        where TValidator : IEventValidator
    {
        var definition = stateMachineDefinitions.Get(
            payload.EventExecutionInfo.StateMachineId
        );

        if (!definition.Events.TryGetValue(
                payload.EventExecutionInfo.EventName,
                out var eventDefinition
            ))
            return [ ];

        return getValidatorNames(eventDefinition)
            .Select(GetValidator<TValidator>)
            .ToList();
    }

    private static TValidator GetValidator<TValidator>(string validatorName)
        where TValidator : IEventValidator
    {
        var validator = EventValidatorContainer.GetEventValidator(validatorName);

        return validator is TValidator typedValidator
            ? typedValidator
            : throw new InvalidOperationException(
                $"Event validator '{validatorName}' does not implement "
                    + $"{typeof(TValidator).Name}."
            );
    }
}
