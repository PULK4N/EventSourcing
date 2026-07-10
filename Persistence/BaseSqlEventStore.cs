using EventSourcing.Persistence.Models;
using EventSourcing.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace EventSourcing.Persistence;

public abstract class BaseSqlEventStore(EventSourcingDbContext applicationDbContext)
{
    public virtual async Task<Dictionary<Guid, EventPayload[]>> GetEvents(
        params Guid[] AggregateIds
    )
    {
        var serializedPayloads = await applicationDbContext
            .SerializedEventPayload
            .Where(x => AggregateIds.Contains(x.AggregateId))
            .AsNoTracking()
            .ToListAsync();

        var payloads = serializedPayloads.Select(x => x.Deserialize());

        var eventsDictionary = new Dictionary<Guid, EventPayload[]>();

        foreach (var aggregateId in AggregateIds)
        {
            var aggregateEvents = payloads
                .Where(x => x.EventExecutionInfo.AggregateId == aggregateId)
                .ToArray();
            eventsDictionary.Add(aggregateId, aggregateEvents);
        }

        return eventsDictionary;
    }

    public async Task Write(bool commit, params EventPayload[] payloads)
    {
        var aggregateIds = payloads.Select(x => x.EventExecutionInfo.AggregateId);
        var serializedPayloads = payloads.Select(SerializedEventPayload.FromPayload);

        await applicationDbContext.SerializedEventPayload.AddRangeAsync(serializedPayloads);
        if (commit)
            await applicationDbContext.SaveChangesAsync();
    }
}
