using EventSourcing.Shared.Interfaces;

namespace EventSourcing.Shared.Models;

public class EventPayload
{
    public EventPayload() { }

    public EventExecutionInfo EventExecutionInfo { get; set; }
    public IEvent EventData { get; set; }
    public List<UniqueEventConstraintData> UniqueEventConstraintsToAdd { get; } = [ ];
    public List<UniqueEventConstraintData> UniqueEventConstraintsToRemove { get; } = [ ];

    public static EventPayload Create(
        EventExecutor eventExecutor,
        AggregateId aggregateId,
        string stateMachineId,
        IEvent eventData
    )
    {
        var payload = new EventPayload
        {
            EventExecutionInfo = new EventExecutionInfo
            {
                EventName = eventData.GetType().Name,
                Timestamp = DateTime.UtcNow,
                StateMachineId = stateMachineId,
                EventExecutor = eventExecutor,
                AggregateId = aggregateId
            },
            EventData = eventData
        };

        return payload;
    }
}
