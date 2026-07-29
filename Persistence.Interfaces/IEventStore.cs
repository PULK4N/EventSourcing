using EventSourcing.Shared.Models;

namespace EventSourcing.Persistence.Interfaces;

public interface IEventStore
{
    Task<Dictionary<AggregateId, List<EventPayload>>> GetEvents(List<AggregateId> AggregateIds);
    Task Write(List<EventPayload> payloads);
}
