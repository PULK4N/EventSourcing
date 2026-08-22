using System.ComponentModel.DataAnnotations;
using EventSourcing.Persistence.Serialization;
using EventSourcing.Shared.Containers;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;

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
        public const int MaxExecutionAttempts = 3;

        public long Id { get; set; }
        public Guid AggregateId { get; set; }
        public string SerializedEventExecutionInfo { get; set; }
        public string SerializedEventData { get; set; }
        public int ExecutionAttempts { get; set; } = 0;
        public MessageStatus Status { get; set; } = MessageStatus.New;
        public string? Error { get; set; }

        [Timestamp]
        public byte[] Version { get; set; }

        public static SerializedPayloadMessage FromPayload(EventPayload payload)
        {
            var serilalizedPayload = new SerializedPayloadMessage
            {
                SerializedEventExecutionInfo = EventJsonSerializer.Serialize(
                    payload.EventExecutionInfo
                ),
                SerializedEventData = EventJsonSerializer.SerializeRuntimeObject(
                    payload.EventData
                ),
                AggregateId = payload.EventExecutionInfo.AggregateId.Value
            };

            return serilalizedPayload;
        }

        public MessagePayload Deserialize()
        {
            var eventExecutionInfo =
                EventJsonSerializer.Deserialize<EventExecutionInfo>(
                this.SerializedEventExecutionInfo
            );

            var eventType = EventTypeContainer.GetEventType(eventExecutionInfo.EventName);

            var eventData = (IEvent)
                EventJsonSerializer.Deserialize(
                    this.SerializedEventData,
                    eventType
                );

            var payload = new EventPayload()
            {
                EventData = eventData,
                EventExecutionInfo = eventExecutionInfo
            };

            return new MessagePayload()
            {
                Payload = payload,
                Id = this.Id,
                ExecutionAttempts = this.ExecutionAttempts
            };
        }

        public void StartReading()
        {
            if (Status != MessageStatus.New)
                throw new InvalidOperationException(
                    $"Only new outbox messages can be read. Message {Id} is {Status}."
                );

            if (ExecutionAttempts >= MaxExecutionAttempts)
                throw new InvalidOperationException(
                    $"Outbox message {Id} has exhausted its retry attempts."
                );

            ++ExecutionAttempts;
            Status = MessageStatus.Reading;
        }

        public void Complete()
        {
            Status = MessageStatus.Sent;
            Error = null;
        }

        public void Fail(string error)
        {
            Error = error;
            Status = ExecutionAttempts >= MaxExecutionAttempts
                ? MessageStatus.Error
                : MessageStatus.New;
        }
    }
}
