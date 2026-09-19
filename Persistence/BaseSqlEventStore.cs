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

    public async Task Write(List<EventPayload> payloads)
    {
        GetConstraintsAndSerializedPayloads(
            payloads,
            out var serializedPayloads,
            out var constraintsToAdd,
            out var constraintsToRemove,
            out var constraintsToTransfer
        );

        await using var transaction = applicationDbContext.Database.CurrentTransaction is null
            ? await applicationDbContext.Database.BeginTransactionAsync()
            : null;

        applicationDbContext.UniqueEventConstraints.RemoveRange(constraintsToRemove);
        await applicationDbContext.SerializedEventPayload.AddRangeAsync(serializedPayloads);
        await applicationDbContext.UniqueEventConstraints.AddRangeAsync(constraintsToAdd);
        await UpdateConstraintTransfer(constraintsToTransfer);
        await applicationDbContext.SaveChangesAsync();

        if (transaction is not null)
            await transaction.CommitAsync();
    }

    private static void GetConstraintsAndSerializedPayloads(
        List<EventPayload> payloads,
        out List<SerializedEventPayload> serializedPayloads,
        out List<UniqueEventConstraint> constraintsToAdd,
        out List<UniqueEventConstraint> constraintsToRemove,
        out List<ConstraintTransfer> constraintsToTransfer
    )
    {
        UniqueEventConstraintValidator.ValidateConditionIsNotAddedThenRemovedInTheSameWrite(
            payloads
        );

        serializedPayloads = payloads.Select(SerializedEventPayload.FromPayload).ToList();
        var constraintsToAddDict = payloads
            .SelectMany(
                payload =>
                    payload
                        .UniqueEventConstraintsToAdd
                        .Select(constraint => new UniqueEventConstraint(payload, constraint))
            )
            .ToDictionary(x => Convert.ToHexString(x.ConstraintHash), x => x);

        var constraintsToRemoveDict = payloads
            .SelectMany(
                payload =>
                    payload
                        .UniqueEventConstraintsToRemove
                        .Select(constraint => new UniqueEventConstraint(payload, constraint))
            )
            .ToDictionary(x => Convert.ToHexString(x.ConstraintHash), x => x);

        constraintsToTransfer = new List<ConstraintTransfer>();
        foreach (var constraintToAddHash in constraintsToAddDict.Keys.ToList())
        {
            if (
                !constraintsToRemoveDict.TryGetValue(
                    constraintToAddHash,
                    out var constraintToRemove
                )
            )
                continue;

            constraintsToTransfer.Add(
                new ConstraintTransfer(
                    constraintToRemove,
                    constraintsToAddDict[constraintToAddHash]
                )
            );

            constraintsToAddDict.Remove(constraintToAddHash);
            constraintsToRemoveDict.Remove(constraintToAddHash);
        }

        constraintsToAdd = constraintsToAddDict.Values.ToList();
        constraintsToRemove = constraintsToRemoveDict.Values.ToList();
    }

    /// <remarks>
    /// Not optimized, but in general it's extremelly rare scenario.
    /// </remarks>
    protected virtual async Task UpdateConstraintTransfer(List<ConstraintTransfer> transfers)
    {
        foreach (var transfer in transfers)
        {
            var affected = await applicationDbContext
                .UniqueEventConstraints
                .Where(
                    constraint =>
                        constraint.ConstraintHash == transfer.Previous.ConstraintHash
                        && constraint.AggregateId == transfer.Previous.AggregateId
                )
                .ExecuteUpdateAsync(
                    setters =>
                        setters
                            .SetProperty(
                                constraint => constraint.AggregateId,
                                transfer.New.AggregateId
                            )
                            .SetProperty(
                                constraint => constraint.OrderNumber,
                                transfer.New.OrderNumber
                            )
                );

            if (affected != 1)
                throw new DbUpdateConcurrencyException(
                    "The constraint is no longer owned by the expected aggregate."
                );
        }
    }
}
