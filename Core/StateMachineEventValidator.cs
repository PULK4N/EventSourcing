using EventSourcing.Shared.Models;

namespace EventSourcing.Core;

public class StateMachineEventValidator()
{
    public static void ValidateSingleStateMachineForAggregate(
        AggregateId aggregateId,
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
}
