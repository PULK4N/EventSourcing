using EventSourcing.Core.Interfaces;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Providers;

public sealed class DefaultUniqueEventConstraintProvider : IUniqueEventConstraintProvider
{
    public IEnumerable<UniqueEventConstraintData> GetConstraintsToAdd(
        object stateData,
        EventPayload payload
    ) => [ ];

    public IEnumerable<UniqueEventConstraintData> GetConstraintsToRemove(
        object stateData,
        EventPayload payload
    ) => [ ];
}
