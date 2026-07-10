using EventSourcing.Persistence.Models;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

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

        var payloads = serializedPayloads.Select(Deserialize);

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

    protected EventPayload Deserialize(SerializedEventPayload serializedPayload)
    {
        var payload = new EventPayload()
        {
            EventExecutionInfo = new EventExecutionInfo()
            {
                AggregateId = serializedPayload.AggregateId,
                EventExecutor = serializedPayload.EventExecutor,
                EventName = serializedPayload.EventName,
                AssemblyQualifiedEventName = serializedPayload.AssemblyQualifiedEventName,
                Id = serializedPayload.Id,
                OrderNumber = serializedPayload.OrderNumber,
                StateMachineId = serializedPayload.StateMachineId,
                Timestamp = serializedPayload.Timestamp
            }
        };

        var eventType = AppDomain
            .CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(
                x => x.AssemblyQualifiedName == serializedPayload.AssemblyQualifiedEventName
            );

        var eventData = (IEvent)
            JsonConvert.DeserializeObject(serializedPayload.SerializedJsonData, eventType);

        payload.EventData = eventData;

        return payload;
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
