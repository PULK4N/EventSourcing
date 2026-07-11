using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;
using Newtonsoft.Json;

namespace EventSourcing.Persistence.Models
{
    public class SerializedEventPayload
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }
        public Guid AggregateId { get; set; }
        public uint OrderNumber { get; set; }
        public Guid EventExecutor { get; set; }
        public string EventName { get; set; } = string.Empty;

        public string AssemblyQualifiedEventName { get; set; } = string.Empty;
        public string StateMachineId { get; set; } = string.Empty;
        public string SerializedJsonData { get; set; } = string.Empty;

        public static SerializedEventPayload FromPayload(EventPayload payload)
        {
            var serilalizedPayload = new SerializedEventPayload
            {
                Id = payload.EventExecutionInfo.Id,
                Timestamp = payload.EventExecutionInfo.Timestamp,
                AggregateId = payload.EventExecutionInfo.AggregateId,
                OrderNumber = payload.EventExecutionInfo.OrderNumber,
                EventExecutor = payload.EventExecutionInfo.EventExecutor,
                EventName = payload.EventExecutionInfo.EventName,
                AssemblyQualifiedEventName = payload.EventExecutionInfo.AssemblyQualifiedEventName,
                StateMachineId = payload.EventExecutionInfo.StateMachineId,
                SerializedJsonData = JsonConvert.SerializeObject(payload.EventData)
            };

            return serilalizedPayload;
        }

        public EventPayload Deserialize()
        {
            var payload = new EventPayload()
            {
                EventExecutionInfo = new EventExecutionInfo()
                {
                    AggregateId = this.AggregateId,
                    EventExecutor = this.EventExecutor,
                    EventName = this.EventName,
                    AssemblyQualifiedEventName = this.AssemblyQualifiedEventName,
                    Id = this.Id,
                    OrderNumber = this.OrderNumber,
                    StateMachineId = this.StateMachineId,
                    Timestamp = this.Timestamp
                }
            };

            var eventType = Type.GetType(
                payload.EventExecutionInfo.AssemblyQualifiedEventName,
                throwOnError: true
            );

            var eventData = (IEvent)
                JsonConvert.DeserializeObject(this.SerializedJsonData, eventType);

            payload.EventData = eventData;

            return payload;
        }
    }
}
