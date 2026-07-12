using EventSourcing.Shared.Models;

namespace EventSourcing.Shared.Containers;

public static class ConstraintCreatorTypeContainer
{
    private static readonly Dictionary<string, IUniqueConstraintCreator> constraintCreators =
        new(StringComparer.Ordinal);

    public static void AddUniqueEventConstraintCreator(Type constraintCreatorType)
    {
        ValidateUniqueEventConstraintCreatorType(constraintCreatorType);

        if (Activator.CreateInstance(constraintCreatorType) is not IUniqueConstraintCreator creator)
            throw new InvalidOperationException(
                $"Constraint creator '{constraintCreatorType.FullName}' must have a public "
                    + "parameterless constructor."
            );

        constraintCreators.Add(constraintCreatorType.Name, creator);
    }

    public static IUniqueConstraintCreator GetUniqueEventConstraintCreator(string name)
    {
        return constraintCreators.TryGetValue(name, out var creator)
            ? creator
            : throw new InvalidOperationException(
                $"Unique constraint creator '{name}' is not registered."
            );
    }

    private static void ValidateUniqueEventConstraintCreatorType(
        Type uniqueEventConstraintCreatorType
    )
    {
        if (!typeof(IUniqueConstraintCreator).IsAssignableFrom(uniqueEventConstraintCreatorType))
            throw new ArgumentException(
                $"Type '{uniqueEventConstraintCreatorType.FullName}' does not implement {nameof(IUniqueConstraintCreator)}.",
                nameof(uniqueEventConstraintCreatorType)
            );

        var creatorName = uniqueEventConstraintCreatorType.Name;

        if (constraintCreators.TryGetValue(creatorName, out var registeredCreator))
        {
            if (registeredCreator.GetType() == uniqueEventConstraintCreatorType)
                throw new InvalidOperationException(
                    $"Duplicate registration of constraint creator "
                        + $"'{uniqueEventConstraintCreatorType.FullName}'."
                );

            throw new InvalidOperationException(
                $"Unique constraint creator name '{creatorName}' is already registered for "
                    + $"'{registeredCreator.GetType().FullName}'."
            );
        }
    }
}
