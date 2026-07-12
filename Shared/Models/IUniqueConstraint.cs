namespace EventSourcing.Shared.Models
{
    public interface IUniqueConstraintCreator<TStateData>
        where TStateData : ISharedStateData
    {
        string Id { get; }

        IEnumerable<UniqueEventConstraintData> CreateConstraintsToRemove(
            TStateData stateBeforeEvent,
            EventPayload payload
        );

        IEnumerable<UniqueEventConstraintData> CreateConstraintsToAdd(
            TStateData stateAfterEvent,
            EventPayload payload
        );
    }
}
