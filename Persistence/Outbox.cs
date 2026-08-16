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

    public async Task<MessagePayload?> ReadNext(CancellationToken cancellationToken = default)
    {
        var serializedMessage = await _applicationDbContext
            .SerializedPayloadMessage
            .Where(
                message =>
                    message.Status == MessageStatus.New
                    && message.ExecutionAttempts < SerializedPayloadMessage.MaxExecutionAttempts
            )
            .OrderBy(message => message.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (serializedMessage is null)
            return null;

        serializedMessage.StartReading();

        try
        {
            await _applicationDbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _applicationDbContext.ChangeTracker.Clear();
            return null;
        }

        return serializedMessage.Deserialize();
    }

    public async Task UpdateCompleted(long id)
    {
        var serializedMessage = await _applicationDbContext
            .SerializedPayloadMessage
            .FirstAsync(x => x.Id == id);

        serializedMessage.Complete();

        _applicationDbContext.Update(serializedMessage);
        await _applicationDbContext.SaveChangesAsync();
    }

    public async Task UpdateFailed(long id)
    {
        var serializedMessage = await _applicationDbContext
            .SerializedPayloadMessage
            .FirstAsync(x => x.Id == id);

        serializedMessage.Fail();

        _applicationDbContext.Update(serializedMessage);
        await _applicationDbContext.SaveChangesAsync();
    }

    public async Task Write(Dictionary<AggregateId, StateInfo> stateInfos)
    {
        var payloads = stateInfos.Values.SelectMany(x => x.LastExecutedPayloads).ToList();
        var serializedPayloadMessages = payloads
            .Select(SerializedPayloadMessage.FromPayload)
            .ToList();

        await _applicationDbContext
            .SerializedPayloadMessage
            .AddRangeAsync(serializedPayloadMessages);
        await _applicationDbContext.SaveChangesAsync();
    }
}
