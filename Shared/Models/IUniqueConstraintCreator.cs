namespace EventSourcing.Shared.Models
{
    public interface IUniqueConstraintCreator
    {
        IEnumerable<UniqueEventConstraintData> CreateConstraintsToRemove(
            object stateBeforeEvent,
            EventPayload payload
        );

        IEnumerable<UniqueEventConstraintData> CreateConstraintsToAdd(
            object stateAfterEvent,
            EventPayload payload
        );
    }

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

        IEnumerable<UniqueEventConstraintData>
            IUniqueConstraintCreator.CreateConstraintsToRemove(
                object stateBeforeEvent,
                EventPayload payload
            ) => CreateConstraintsToRemove((TStateData)stateBeforeEvent, payload);

        IEnumerable<UniqueEventConstraintData> IUniqueConstraintCreator.CreateConstraintsToAdd(
            object stateAfterEvent,
            EventPayload payload
        ) => CreateConstraintsToAdd((TStateData)stateAfterEvent, payload);
    }
}
