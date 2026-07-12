namespace EventSourcing.Shared.Models
{
    public interface IUniqueConstraintCreator { }

    public interface IUniqueConstraintCreator<TStateData> : IUniqueConstraintCreator
        where TStateData : ISharedStateData
    {
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
