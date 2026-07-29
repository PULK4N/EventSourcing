using EventSourcing.Shared.Containers;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;
using Newtonsoft.Json;

namespace EventSourcing.Persistence.Models
{
    public class SerializedEventPayload
    {
        public DateTime Timestamp { get; set; }
        public Guid AggregateId { get; set; }
        public uint OrderNumber { get; set; }
        public Guid EventExecutor { get; set; }
        public string EventName { get; set; } = string.Empty;

        public string StateMachineId { get; set; } = string.Empty;
        public string SerializedJsonData { get; set; } = string.Empty;

        public static SerializedEventPayload FromPayload(EventPayload payload)
        {
            var serilalizedPayload = new SerializedEventPayload
            {
                Timestamp = payload.EventExecutionInfo.Timestamp,
                AggregateId = payload.EventExecutionInfo.AggregateId.Value,
                OrderNumber = payload.EventExecutionInfo.OrderNumber,
                EventExecutor = payload.EventExecutionInfo.EventExecutor.Value,
                EventName = payload.EventExecutionInfo.EventName,
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
                    AggregateId = EventSourcing
                        .Shared
                        .Models
                        .AggregateId
                        .FromDatabaseGuid(this.AggregateId),
                    EventExecutor = EventSourcing
                        .Shared
                        .Models
                        .EventExecutor
                        .FromDatabaseGuid(this.EventExecutor),
                    EventName = this.EventName,
                    OrderNumber = this.OrderNumber,
                    StateMachineId = this.StateMachineId,
                    Timestamp = this.Timestamp
                }
            };

            var eventType = EventTypeContainer.GetEventType(EventName);

            var eventData = (IEvent)
                JsonConvert.DeserializeObject(this.SerializedJsonData, eventType)!;

            payload.EventData = eventData;

            return payload;
        }
    }
}
