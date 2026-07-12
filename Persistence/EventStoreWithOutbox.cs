using EventSourcing.Persistence.Interfaces;
using EventSourcing.Persistence.Models;
using EventSourcing.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace EventSourcing.Persistence;

public class EventStoreWithOutbox(
    EventSourcingDbContext applicationDbContext,
    IEventStore _eventStore,
    IOutbox _outbox
) : IEventStoreWithOutbox
{
    public async Task Write(params EventPayload[] payloads)
    {
        using var transaction = await applicationDbContext.Database.BeginTransactionAsync();
        await _eventStore.Write(payloads);
        await _outbox.Write(payloads);
        transaction.Commit();
    }

    public Task<Dictionary<Guid, EventPayload[]>> GetEvents(params Guid[] AggregateId) =>
        _eventStore.GetEvents(AggregateId);
}
