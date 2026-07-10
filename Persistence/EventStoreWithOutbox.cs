using EventSourcing.Core.Interfaces;
using EventSourcing.Persistence.Models;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Newtonsoft.Json;

namespace EventSourcing.Persistence;

public class EventStoreWithOutbox : IEventStoreWithOutbox
{
    protected readonly EventSourcingDbContext _applicationDbContext;

    public EventStoreWithOutbox(EventSourcingDbContext applicationDbContext)
    {
        _applicationDbContext = applicationDbContext;
    }

    public virtual async Task<Dictionary<Guid, EventPayload[]>> GetEventsByAggregate(
        params Guid[] AggregateIds
    )
    {
        var serializedPayloads = await _applicationDbContext
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

    public async Task WriteEventsWithOutbox(params EventPayload[] payloads)
    {
        var aggregateIds = payloads.Select(x => x.EventExecutionInfo.AggregateId);
        var serializedPayloads = payloads.Select(SerializedEventPayload.FromPayload);

        var serializedPayloadMessages = payloads.Select(SerializedPayloadMessage.FromPayload);

        await _applicationDbContext
            .SerializedPayloadMessage
            .AddRangeAsync(serializedPayloadMessages);
        await _applicationDbContext.SerializedEventPayload.AddRangeAsync(serializedPayloads);
        await _applicationDbContext.SaveChangesAsync();
    }

    // Can be rewritten to work with batches
    public async Task<MessagePayload> GetLatestMessage()
    {
        var serializedMessage = await _applicationDbContext
            .SerializedPayloadMessage
            .Where(x => x.Status == MessageStatus.New)
            .FirstOrDefaultAsync();

        if (serializedMessage is null)
            return null;

        serializedMessage.Status = MessageStatus.Reading;

        _applicationDbContext.Update(serializedMessage);
        await _applicationDbContext.SaveChangesAsync();

        return Deserialize(serializedMessage);
    }

    private MessagePayload Deserialize(SerializedPayloadMessage serializedPayload)
    {
        var eventExecutionInfo = JsonConvert.DeserializeObject<EventExecutionInfo>(
            serializedPayload.SerializedEventExecutionInfo
        );

        var eventType = AppDomain
            .CurrentDomain
            .GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .FirstOrDefault(
                x => x.AssemblyQualifiedName == eventExecutionInfo.AssemblyQualifiedEventName
            );

        var eventData = (IEvent)
            JsonConvert.DeserializeObject(serializedPayload.SerializedEventData, eventType);

        var payload = new EventPayload()
        {
            EventData = eventData,
            EventExecutionInfo = eventExecutionInfo
        };

        return new MessagePayload() { Payload = payload, Id = serializedPayload.Id };
    }

    public async Task UpdateCompleted(long id)
    {
        var serializedMessage = await _applicationDbContext
            .SerializedPayloadMessage
            .FirstAsync(x => x.Id == id);

        ++serializedMessage.ExecutionAttempts;
        serializedMessage.Status = MessageStatus.Sent;

        _applicationDbContext.Update(serializedMessage);
        await _applicationDbContext.SaveChangesAsync();
    }

    public async Task UpdateFailed(long id)
    {
        var serializedMessage = await _applicationDbContext
            .SerializedPayloadMessage
            .FirstAsync(x => x.Id == id);

        ++serializedMessage.ExecutionAttempts;
        serializedMessage.Status = MessageStatus.New;

        _applicationDbContext.Update(serializedMessage);
        await _applicationDbContext.SaveChangesAsync();
    }

    Task<MessagePayload> GetEventsWithLatestOrderNumber(Guid aggregateId, uint orderNumber)
    {
        var serializedMessage = await _applicationDbContext
            .SerializedPayloadMessage
            .FirstAsync(x => x.Id == id);

        ++serializedMessage.ExecutionAttempts;
        serializedMessage.Status = MessageStatus.New;

        _applicationDbContext.Update(serializedMessage);
        await _applicationDbContext.SaveChangesAsync();
    }
}
