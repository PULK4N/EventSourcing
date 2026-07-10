using EventSourcing.Shared.Models;

public class MessagePayload
{
    public EventPayload Payload { get; set; }
    public long Id { get; set; }
}
