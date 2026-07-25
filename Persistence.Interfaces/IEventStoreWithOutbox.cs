using EventSourcing.Shared.Models;

namespace EventSourcing.Persistence.Interfaces;

public interface IEventStoreWithOutbox
{
    Task Write(params EventPayload[] payloads);

    Task<Dictionary<AggregateId, EventPayload[]>> GetEvents(params AggregateId[] AggregateId);
}
