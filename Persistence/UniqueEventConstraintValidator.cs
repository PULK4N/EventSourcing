using EventSourcing.Persistence.Models;
using EventSourcing.Shared.Models;

namespace EventSourcing.Persistence;

internal static class UniqueEventConstraintValidator
{
    internal static void ValidateConditionIsNotAddedThenRemovedInTheSameWrite(
        List<EventPayload> allPayloads
    )
    {
        var allConstraintsToAdd = ValidateAndGetConstraints(allPayloads, "add");
        var allConstraintsToRemove = ValidateAndGetConstraints(allPayloads, "remove");

        foreach (var aggregateId in allConstraintsToAdd.Keys)
        {
            if (
                allConstraintsToAdd[aggregateId].Any(x =>
                {
                    if (
                        !allConstraintsToRemove.TryGetValue(
                            aggregateId,
                            out var constraintsToRemove
                        )
                    )
                        return false;

                    return constraintsToRemove.Contains(x);
                })
            )
                throw new InvalidOperationException(
                    $"Unique constraint was added and then removed within the same write."
                );
        }
    }

    private readonly record struct HashDetails(
        Guid AggregateId,
        string StateMachineId,
        string ConstraintName,
        string Hash
    );

    private static Dictionary<Guid, HashSet<HashDetails>> ValidateAndGetConstraints(
        List<EventPayload> allPayloads,
        string addOrRemove
    )
    {
        var allConstraints = allPayloads
            .SelectMany(
                x =>
                    addOrRemove == "add"
                        ? x.UniqueEventConstraintsToAdd.Select(
                            constraint => new UniqueEventConstraint(x, constraint)
                        )
                        : x.UniqueEventConstraintsToRemove.Select(
                            constraint => new UniqueEventConstraint(x, constraint)
                        )
            )
            .Select(
                x =>
                    new HashDetails(
                        x.AggregateId,
                        x.StateMachineId,
                        x.ConstraintName,
                        Convert.ToHexString(x.ConstraintHash)
                    )
            )
            .ToList();
        var distinctConstraints = allConstraints
            .Select(x => new StateMachineWithHash(x.StateMachineId, x.Hash))
            .ToHashSet();
        if (distinctConstraints.Count != allConstraints.Count)
        {
            var duplicates = allConstraints
                .GroupBy(x => x.Hash)
                .Where(x => x.Count() > 1)
                .Select(x => x.First().ConstraintName);
            throw new InvalidOperationException(
                $"Error, Multiple {addOrRemove} constraints for constraints {string.Join(",", duplicates)}"
            );
        }

        return allConstraints
            .GroupBy(x => x.AggregateId)
            .ToDictionary(x => x.Key, x => x.ToHashSet());
    }

    private readonly record struct StateMachineWithHash(string StateMachineId, string Hash);
}
