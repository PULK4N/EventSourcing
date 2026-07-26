using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Models;

namespace EventSourcing.Persistence;

public class EventStoreWithOutbox(
    EventSourcingDbContext applicationDbContext,
    IEventStore _eventStore,
    IOutbox _outbox
) : IEventStoreWithOutbox
{
    public async Task Write(List<EventPayload> payloads)
    {
        using var transaction = await applicationDbContext.Database.BeginTransactionAsync();
        await _eventStore.Write(payloads);
        await _outbox.Write(payloads);
        await transaction.CommitAsync();
    }

    public Task<Dictionary<AggregateId, EventPayload[]>> GetEvents(
        params AggregateId[] AggregateId
    ) => _eventStore.GetEvents(AggregateId);
}
