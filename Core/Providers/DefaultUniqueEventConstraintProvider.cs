using EventSourcing.Core.Interfaces;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Providers;

public sealed class DefaultUniqueEventConstraintProvider : IUniqueEventConstraintProvider
{
    public IEnumerable<UniqueEventConstraintData> GetConstraintsToAdd(EventPayload payload) => [ ];

    public IEnumerable<UniqueEventConstraintData> GetConstraintsToRemove(EventPayload payload) =>
        [ ];
}
