using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Models;

namespace EventSourcing.Persistence;

public class EventStore(BaseSqlEventStore baseSqlEventStore) : IEventStore
{
    public virtual Task<Dictionary<AggregateId, EventPayload[]>> GetEvents(
        params AggregateId[] AggregateIds
    ) => baseSqlEventStore.GetEvents(AggregateIds);

    public async Task Write(List<EventPayload> payloads)
    {
        await baseSqlEventStore.Write(payloads);
    }
}
