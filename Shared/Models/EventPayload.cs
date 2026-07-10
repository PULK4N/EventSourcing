using EventSourcing.Shared.Interfaces;

namespace EventSourcing.Shared.Models
{
    public class EventPayload
    {
        public EventPayload() { }

        public EventExecutionInfo EventExecutionInfo { get; set; }
        public IEvent EventData { get; set; }

        public static EventPayload Create(
            Guid eventExecutor,
            Guid aggregateId,
            string stateMachineId,
            IEvent eventData
        )
        {
            var payload = new EventPayload
            {
                EventExecutionInfo = new EventExecutionInfo
                {
                    Id = Guid.NewGuid(),
                    EventName = eventData.GetType().Name,
                    AssemblyQualifiedEventName = eventData.GetType().AssemblyQualifiedName,
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
}
