using EventSourcing.Persistence.Interfaces;
using EventSourcing.Persistence.Models;
using EventSourcing.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace EventSourcing.Persistence;

public class Outbox : IOutbox
{
    protected readonly EventSourcingDbContext _applicationDbContext;

    public Outbox(EventSourcingDbContext applicationDbContext)
    {
        _applicationDbContext = applicationDbContext;
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

    public async Task Write(List<EventPayload> payloads)
    {
        var aggregateIds = payloads.Select(x => x.EventExecutionInfo.AggregateId);

        var serializedPayloadMessages = payloads.Select(SerializedPayloadMessage.FromPayload);

        await _applicationDbContext
            .SerializedPayloadMessage
            .AddRangeAsync(serializedPayloadMessages);
        await _applicationDbContext.SaveChangesAsync();
    }
}
