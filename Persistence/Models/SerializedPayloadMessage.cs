using System.ComponentModel.DataAnnotations;
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
            var serilalizedPayload = new SerializedPayloadMessage();

            serilalizedPayload.SerializedEventExecutionInfo = JsonConvert.SerializeObject(
                payload.EventExecutionInfo
            );
            serilalizedPayload.SerializedEventData = JsonConvert.SerializeObject(payload.EventData);
            serilalizedPayload.AggregateId = payload.EventExecutionInfo.AggregateId;

            return serilalizedPayload;
        }
    }
}
