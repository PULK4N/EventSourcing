using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Interfaces
{
    public interface IEventStore
    {
        Task<Dictionary<Guid, EventPayload[]>> GetEvents(params Guid[] AggregateId);
        Task Write(params EventPayload[] payloads);
    }
}
