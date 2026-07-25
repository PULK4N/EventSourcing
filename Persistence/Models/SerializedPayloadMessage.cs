using System.ComponentModel.DataAnnotations;
using EventSourcing.Shared.Containers;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;
using Newtonsoft.Json;

namespace EventSourcing.Persistence.Models
{
    public enum MessageStatus
    {
        New,
        Reading,
        Error,
        Sent
    }

    public class SerializedPayloadMessage
    {
        public long Id { get; set; }
        public Guid AggregateId { get; set; }
        public string SerializedEventExecutionInfo { get; set; }
        public string SerializedEventData { get; set; }
        public int ExecutionAttempts { get; set; } = 0;
        public MessageStatus Status { get; set; } = MessageStatus.New;

        [Timestamp]
        public byte[] Version { get; set; }

        public static SerializedPayloadMessage FromPayload(EventPayload payload)
        {
            var serilalizedPayload = new SerializedPayloadMessage
            {
                SerializedEventExecutionInfo = JsonConvert.SerializeObject(
                    payload.EventExecutionInfo
                ),
                SerializedEventData = JsonConvert.SerializeObject(payload.EventData),
                AggregateId = payload.EventExecutionInfo.AggregateId.Value
            };

            return serilalizedPayload;
        }

        public MessagePayload Deserialize()
        {
            var eventExecutionInfo = JsonConvert.DeserializeObject<EventExecutionInfo>(
                this.SerializedEventExecutionInfo
            );

            var eventType = EventTypeContainer.GetEventType(eventExecutionInfo.EventName);

            var eventData = (IEvent)
                JsonConvert.DeserializeObject(this.SerializedEventData, eventType);

            var payload = new EventPayload()
            {
                EventData = eventData,
                EventExecutionInfo = eventExecutionInfo
            };

            return new MessagePayload() { Payload = payload, Id = this.Id };
        }
    }
}
