using EventSourcing.Core.Interfaces;
using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Helpers;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core;

public sealed class ProjectionOutbox : IOutbox
{
    private readonly IStateMachineDefinitionProvider _stateMachineDefinitions;
    private readonly IEventStore _eventStore;
    private readonly StateCalculator _stateCalculator;
    private readonly IReadOnlyDictionary<string, IProjector> _projectors;

    public ProjectionOutbox(
        IStateMachineDefinitionProvider stateMachineDefinitions,
        StateCalculator stateCalculator,
        IEventStore eventStore,
        IEnumerable<IProjector> projectors
    )
    {
        _stateMachineDefinitions = stateMachineDefinitions;
        _stateCalculator = stateCalculator;
        _eventStore = eventStore;
        _projectors = CreateProjectorDictionary(projectors);
    }

    public Task UpdateCompleted(long id) => Task.CompletedTask;

    public Task UpdateFailed(long id) => Task.CompletedTask;

    public async Task Write(Dictionary<AggregateId, StateInfo> stateInfos)
    {
        var aggregatePayloadsDict = stateInfos.ToDictionary(
            x => x.Key,
            x => x.Value.LastExecutedPayloads
        );

        var projectorsByAggregate = CollectProjectorsPerAggregate(aggregatePayloadsDict);

        foreach (var (projectorName, aggregateIds) in projectorsByAggregate)
        {
            var projector = _projectors[projectorName];
            var projectorStateInfos = stateInfos
                .Where(x => aggregateIds.Contains(x.Key))
                .Select(x => x.Value)
                .ToList();
            await projector.Update(projectorStateInfos);
        }
    }

    private Dictionary<string, HashSet<AggregateId>> CollectProjectorsPerAggregate(
        Dictionary<AggregateId, List<EventPayload>> aggregatePayloadsDict
    )
    {
        var projectorNameAggregateDict = new Dictionary<string, HashSet<AggregateId>>();
        foreach (var aggregate in aggregatePayloadsDict.Keys)
        {
            var aggregatePayloads = aggregatePayloadsDict[aggregate];
            var payload = aggregatePayloads[0];
            var definition = _stateMachineDefinitions.Get(
                payload.EventExecutionInfo.StateMachineId
            );

            var defaultProjectorNames = definition.Projections;

            var eventProjectorNames = aggregatePayloads
                .SelectMany(eventPayload =>
                {
                    if (
                        definition
                            .Events
                            .TryGetValue(
                                eventPayload.EventExecutionInfo.EventName,
                                out var eventDefinition
                            )
                    )
                        return eventDefinition.Projections;

                    return [ ];
                })
                .ToList();

            var projectorNames = defaultProjectorNames
                .Concat(eventProjectorNames)
                .Distinct()
                .ToList();

            foreach (var projector in projectorNames)
            {
                if (projectorNameAggregateDict.ContainsKey(projector))
                    projectorNameAggregateDict[projector].Add(aggregate);
                else
                    projectorNameAggregateDict[projector] =  [ aggregate ];
            }
        }

        return projectorNameAggregateDict;
    }

    private static IReadOnlyDictionary<string, IProjector> CreateProjectorDictionary(
        IEnumerable<IProjector> projectors
    )
    {
        var result = new Dictionary<string, IProjector>(StringComparer.Ordinal);

        projectors = projectors.ToList();
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
