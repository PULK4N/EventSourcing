using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Interfaces;

public interface IUniqueEventConstraintProvider
{
    IEnumerable<UniqueEventConstraintData> GetConstraintsToAdd(
        object stateData,
        EventPayload payload
    );
    IEnumerable<UniqueEventConstraintData> GetConstraintsToRemove(
        object stateData,
        EventPayload payload
    );
}
