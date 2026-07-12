using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Interfaces;

public interface IUniqueEventConstraintProvider
{
    IEnumerable<UniqueEventConstraintData> GetConstraintsToAdd(EventPayload payload);
    IEnumerable<UniqueEventConstraintData> GetConstraintsToRemove(EventPayload payload);
}
