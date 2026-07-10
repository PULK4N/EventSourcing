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
            var serilalizedPayload = new SerializedEventPayload();

            serilalizedPayload.Id = payload.EventExecutionInfo.Id;
            serilalizedPayload.Timestamp = payload.EventExecutionInfo.Timestamp;
            serilalizedPayload.AggregateId = payload.EventExecutionInfo.AggregateId;
            serilalizedPayload.OrderNumber = payload.EventExecutionInfo.OrderNumber;
            serilalizedPayload.EventExecutor = payload.EventExecutionInfo.EventExecutor;
            serilalizedPayload.EventName = payload.EventExecutionInfo.EventName;
            serilalizedPayload.AssemblyQualifiedEventName = payload
                .EventExecutionInfo
                .AssemblyQualifiedEventName;
            serilalizedPayload.StateMachineId = payload.EventExecutionInfo.StateMachineId;

            serilalizedPayload.SerializedJsonData = JsonConvert.SerializeObject(payload.EventData);

            return serilalizedPayload;
        }
    }
}
