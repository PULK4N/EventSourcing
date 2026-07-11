using EventSourcing.Persistence.Interfaces;
using EventSourcing.Persistence.Models;
using EventSourcing.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace EventSourcing.Persistence;

public class EventStoreWithOutbox(
    BaseSqlEventStore baseSqlEventStore,
    IOutbox outbox,
    EventSourcingDbContext applicationDbContext
) : IEventStore
{
    public virtual Task<Dictionary<Guid, EventPayload[]>> GetEvents(params Guid[] AggregateIds) =>
        baseSqlEventStore.GetEvents(AggregateIds);

    public async Task Write(params EventPayload[] payloads)
    {
        await baseSqlEventStore.Write(false, payloads);
        await outbox.Write(payloads);
        await applicationDbContext.SaveChangesAsync();
    }

    // Can be rewritten to work with batches
    public async Task<MessagePayload> GetLatestMessage()
    {
        var serializedMessage = await applicationDbContext
            .SerializedPayloadMessage
            .Where(x => x.Status == MessageStatus.New)
            .FirstOrDefaultAsync();

        if (serializedMessage is null)
            return null;

        serializedMessage.Status = MessageStatus.Reading;

        applicationDbContext.Update(serializedMessage);
        await applicationDbContext.SaveChangesAsync();

        return serializedMessage.Deserialize();
    }
}
