using EventSourcing.Shared.Models;

namespace EventSourcing.Shared.Interfaces;

public interface IUniqueConstraintCreator
{
    Type StateDataType { get; }

    List<UniqueConstraint> Create(object stateData);
}

/// <summary>
/// Creates the <typeparamref name="TConstraint"/> values that a state currently holds.
/// Return an empty list when the state holds no value for this constraint.
/// </summary>
public interface IUniqueConstraintCreator<TStateData, TConstraint> : IUniqueConstraintCreator
    where TStateData : ISharedStateData
    where TConstraint : UniqueConstraint
{
    List<TConstraint> Create(TStateData stateData);

    Type IUniqueConstraintCreator.StateDataType => typeof(TStateData);

    List<UniqueConstraint> IUniqueConstraintCreator.Create(object stateData) =>
        [ .. Create((TStateData)stateData) ];
}
