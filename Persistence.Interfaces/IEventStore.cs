using EventSourcing.Shared.Models;

namespace EventSourcing.Persistence.Interfaces;

public interface IEventStore
{
    Task<Dictionary<AggregateId, EventPayload[]>> GetEvents(params AggregateId[] AggregateId);
    Task Write(List<EventPayload> payloads);
}
