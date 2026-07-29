using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Models;

namespace EventSourcing.Persistence;

public class EventStore(BaseSqlEventStore baseSqlEventStore) : IEventStore
{
    public virtual Task<Dictionary<AggregateId, List<EventPayload>>> GetEvents(
        List<AggregateId> aggregateIds
    ) => baseSqlEventStore.GetEvents(aggregateIds);

    public async Task Write(List<EventPayload> payloads)
    {
        await baseSqlEventStore.Write(payloads);
    }
}
