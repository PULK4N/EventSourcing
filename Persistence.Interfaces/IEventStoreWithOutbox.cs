using EventSourcing.Shared.Models;

namespace EventSourcing.Persistence.Interfaces;

public interface IEventStoreWithOutbox
{
    Task Write(params EventPayload[] payloads);

    Task<Dictionary<Guid, EventPayload[]>> GetEvents(params Guid[] AggregateId);
}
