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

    public async Task Write(params EventPayload[] payloads)
    {
        GetConstraintsAndSerializedPayloads(
            payloads,
            out var serializedPayloads,
            out var constraintsToAdd,
            out var constraintsToRemove
        );

        applicationDbContext.UniqueEventConstraints.RemoveRange(constraintsToRemove);
        await applicationDbContext.SerializedEventPayload.AddRangeAsync(serializedPayloads);
        await applicationDbContext.UniqueEventConstraints.AddRangeAsync(constraintsToAdd);
        await applicationDbContext.SaveChangesAsync();
    }

    private static void GetConstraintsAndSerializedPayloads(
        EventPayload[] payloads,
        out List<SerializedEventPayload> serializedPayloads,
        out List<UniqueEventConstraint> constraintsToAdd,
        out List<UniqueEventConstraint> constraintsToRemove
    )
    {
        serializedPayloads = payloads.Select(SerializedEventPayload.FromPayload).ToList();
        constraintsToAdd = payloads
            .SelectMany(
                payload =>
                    payload
                        .UniqueEventConstraintsToAdd
                        .Select(constraint => new UniqueEventConstraint(payload, constraint))
            )
            .ToList();
        constraintsToRemove = payloads
            .SelectMany(
                payload =>
                    payload
                        .UniqueEventConstraintsToRemove
                        .Select(constraint => new UniqueEventConstraint(payload, constraint))
            )
            .ToList();
        var duplicateHashes = constraintsToRemove
            .Select(x => Convert.ToHexString(x.ConstraintHash))
            .Intersect(
                constraintsToAdd.Select(x => Convert.ToHexString(x.ConstraintHash)),
                StringComparer.Ordinal
            )
            .ToArray();

        if (duplicateHashes.Length > 0)
            throw new InvalidOperationException(
                "A unique constraint cannot be added and removed in the same write."
            );
    }
}
