using System.Linq.Expressions;
using EventSourcing.Persistence.Models;
using EventSourcing.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace EventSourcing.Persistence;

public class BaseSqlEventStore(EventSourcingDbContext applicationDbContext)
{
    public async Task<Dictionary<Guid, EventPayload[]>> GetEvents(params Guid[] AggregateIds)
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

    public async Task Write(bool commit, params EventPayload[] payloads)
    {
        var serializedPayloads = payloads.Select(SerializedEventPayload.FromPayload);
        var constraintsToAdd = payloads.SelectMany(
            payload =>
                payload
                    .UniqueEventConstraintsToAdd
                    .Select(constraint => new UniqueEventConstraint(payload, constraint))
        );
        var constraintsToRemove = payloads.SelectMany(
            payload =>
                payload
                    .UniqueEventConstraintsToRemove
                    .Select(constraint => new UniqueEventConstraint(payload, constraint))
        );

        await applicationDbContext.SerializedEventPayload.AddRangeAsync(serializedPayloads);
        applicationDbContext.UniqueEventConstraints.RemoveRange(constraintsToRemove);
        await applicationDbContext.UniqueEventConstraints.AddRangeAsync(constraintsToAdd);

        if (commit)
            await applicationDbContext.SaveChangesAsync();
    }
}
