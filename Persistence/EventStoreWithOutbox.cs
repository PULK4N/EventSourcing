using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Models;

namespace EventSourcing.Persistence;

public class EventStoreWithOutbox(
    EventSourcingDbContext applicationDbContext,
    IEventStore _eventStore,
    IOutbox _outbox
) : IEventStoreWithOutbox
{
    public async Task Write(Dictionary<AggregateId, StateInfo> stateInfos)
    {
        var payloads = stateInfos.Values.SelectMany(x => x.LastExecutedPayloads).ToList();
        using var transaction = await applicationDbContext.Database.BeginTransactionAsync();
        await _eventStore.Write(payloads);
        await _outbox.Write(stateInfos);
        await transaction.CommitAsync();
    }

    public Task<Dictionary<AggregateId, List<EventPayload>>> GetEvents(
        List<AggregateId> aggregateIds
    ) => _eventStore.GetEvents(aggregateIds);
}
