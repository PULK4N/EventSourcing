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

    public async Task Write(List<StateInfo> stateInfos)
    {
        var serializedPayloads = stateInfos
            .SelectMany(stateInfo => stateInfo.LastExecutedPayloads)
            .Select(SerializedEventPayload.FromPayload)
            .ToList();

        var stateInfosWithConstraintsChanged = stateInfos
            .Where(x => x.UniqueConstraintsChanged)
            .ToList();
        var uniqueConstraints = stateInfosWithConstraintsChanged
            .SelectMany(stateInfo => stateInfo.UniqueConstraints)
            .ToList();
        var constraintsChangedAggregates = stateInfosWithConstraintsChanged
            .Select(x => x.AggregateId.Value)
            .ToList();

        var constraintsWereUpdated = stateInfosWithConstraintsChanged.Any();

        await using var transaction =
            applicationDbContext.Database.CurrentTransaction is null && constraintsWereUpdated
                ? await applicationDbContext.Database.BeginTransactionAsync()
                : null;

        await applicationDbContext.SerializedEventPayload.AddRangeAsync(serializedPayloads);

        if (constraintsWereUpdated)
        {
            await applicationDbContext
                .UniqueConstraints
                .Where(constraint => constraintsChangedAggregates.Contains(constraint.AggregateId))
                .ExecuteDeleteAsync();
            await applicationDbContext.UniqueConstraints.AddRangeAsync(uniqueConstraints);
        }

        await applicationDbContext.SaveChangesAsync();

        if (transaction is not null)
            await transaction.CommitAsync();
    }
}
