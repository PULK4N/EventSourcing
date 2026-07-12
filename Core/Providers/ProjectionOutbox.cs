using EventSourcing.Core.Interfaces;
using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Providers;

public sealed class ProjectionOutbox : IOutbox
{
    private readonly IStateMachineDefinitionProvider _stateMachineDefinitions;
    private readonly IReadOnlyDictionary<string, IEventProjector> _projectors;

    public ProjectionOutbox(
        IStateMachineDefinitionProvider stateMachineDefinitions,
        IEnumerable<IEventProjector> projectors
    )
    {
        _stateMachineDefinitions = stateMachineDefinitions;
        _projectors = CreateProjectorDictionary(projectors);
    }

    public Task UpdateCompleted(long id) => Task.CompletedTask;

    public Task UpdateFailed(long id) => Task.CompletedTask;

    public async Task Write(params EventPayload[] payloads)
    {
        ArgumentNullException.ThrowIfNull(payloads);

        var payloadsByProjector = CollectPayloadsByProjector(payloads);

        foreach (var (projector, projectorPayloads) in payloadsByProjector)
            await projector.Update(projectorPayloads.ToArray());
    }

    private Dictionary<IEventProjector, List<EventPayload>> CollectPayloadsByProjector(
        IEnumerable<EventPayload> payloads
    )
    {
        var result = new Dictionary<IEventProjector, List<EventPayload>>();

        foreach (var payload in payloads)
        {
            var executionInfo = payload.EventExecutionInfo;
            var definition = _stateMachineDefinitions.Get(executionInfo.StateMachineId);

            var projectionIds = definition.Projections.AsEnumerable();

            if (definition.Events.TryGetValue(executionInfo.EventName, out var eventDefinition))
                projectionIds = projectionIds.Concat(eventDefinition.Projections);

            foreach (var projectionId in projectionIds.Distinct(StringComparer.Ordinal))
            {
                if (!_projectors.TryGetValue(projectionId, out var projector))
                    throw new InvalidOperationException(
                        $"Projector '{projectionId}' is not registered."
                    );

                if (!result.TryGetValue(projector, out var projectorPayloads))
                {
                    projectorPayloads = [ ];
                    result.Add(projector, projectorPayloads);
                }

                projectorPayloads.Add(payload);
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<string, IEventProjector> CreateProjectorDictionary(
        IEnumerable<IEventProjector> projectors
    )
    {
        var result = new Dictionary<string, IEventProjector>(StringComparer.Ordinal);

        foreach (var projector in projectors)
        {
            var projectorName = projector.GetType().Name;

            if (!result.TryAdd(projectorName, projector))
                throw new InvalidOperationException(
                    $"Multiple projectors are registered with name '{projectorName}'."
                );
        }

        return result;
    }
}
