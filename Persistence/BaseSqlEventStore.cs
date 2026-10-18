using System.Linq.Expressions;
using EventSourcing.Persistence.Models;
using EventSourcing.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace EventSourcing.Persistence;

public class BaseSqlEventStore(EventSourcingDbContext applicationDbContext)
{
    public async Task<Dictionary<AggregateId, List<EventPayload>>> GetEvents(
        List<AggregateId> aggregateIds
    )
    {
        var aggregateGuids = aggregateIds.Select(x => x.Value).Distinct();
        var serializedPayloads = await applicationDbContext
            .SerializedEventPayload
            .Where(x => aggregateGuids.Contains(x.AggregateId))
            .AsNoTracking()
            .ToListAsync();

        var payloads = serializedPayloads.Select(x => x.Deserialize()).ToList();

        var eventsDictionary = new Dictionary<AggregateId, List<EventPayload>>();

        foreach (var aggregateId in aggregateIds)
        {
            var aggregateEvents = payloads
                .Where(x => x.EventExecutionInfo.AggregateId == aggregateId)
                .OrderBy(x => x.EventExecutionInfo.OrderNumber)
                .ToList();
            eventsDictionary.Add(aggregateId, aggregateEvents);
        }

        return eventsDictionary;
    }

    public async Task<List<EventPayload>> GetEvents(
        Expression<Func<SerializedEventPayload, bool>> predicate
    )
    {
        var serializedPayloads = await applicationDbContext
            .SerializedEventPayload
            .Where(predicate)
            .AsNoTracking()
            .ToListAsync();

        return serializedPayloads.Select(x => x.Deserialize()).ToList();
    }

    /// <summary>
    /// Writes the new events and replaces the unique constraints of every written aggregate
    /// with the ones its final state holds. The delete runs first so that a value can move
    /// between aggregates in one write; the unique hash index rejects any value already held
    /// by another aggregate.
    /// </summary>
    public async Task Write(List<StateInfo> stateInfos)
    {
        var serializedPayloads = stateInfos
            .SelectMany(stateInfo => stateInfo.LastExecutedPayloads)
            .Select(SerializedEventPayload.FromPayload)
            .ToList();
        var uniqueConstraints = stateInfos
            .SelectMany(stateInfo => stateInfo.UniqueConstraints)
            .ToList();
        var aggregateIds = stateInfos.Select(stateInfo => stateInfo.AggregateId.Value).ToList();

        await using var transaction = applicationDbContext.Database.CurrentTransaction is null
            ? await applicationDbContext.Database.BeginTransactionAsync()
            : null;

        await applicationDbContext
            .UniqueConstraints
            .Where(constraint => aggregateIds.Contains(constraint.AggregateId))
            .ExecuteDeleteAsync();
        await applicationDbContext.SerializedEventPayload.AddRangeAsync(serializedPayloads);
        await applicationDbContext.UniqueConstraints.AddRangeAsync(uniqueConstraints);
        await applicationDbContext.SaveChangesAsync();

        if (transaction is not null)
            await transaction.CommitAsync();
    }
}
