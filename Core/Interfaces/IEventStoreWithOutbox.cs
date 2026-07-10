using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Interfaces
{
    public interface IEventStoreWithOutbox
    {
        Task<Dictionary<Guid, EventPayload[]>> GetEventsByAggregate(params Guid[] AggregateId);

        Task WriteEventsWithOutbox(params EventPayload[] payloads);
        Task<MessagePayload> GetLatestMessage();
        Task<MessagePayload> GetEventsWithLatestOrderNumber(Guid aggregateId, uint orderNumber);
        Task UpdateCompleted(long id);
        Task UpdateFailed(long id);
    }
}
