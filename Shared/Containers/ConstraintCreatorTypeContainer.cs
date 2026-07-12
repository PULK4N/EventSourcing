using EventSourcing.Shared.Exceptions;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;

namespace EventSourcing.Shared.Containers;

public static class ConstraintCreatorTypeContainer
{
    private static readonly Dictionary<string, Type> eventTypes = new(StringComparer.Ordinal);

    public static void AddUniqueEventConstraintCreatorType(Type uniqueEventConstraintCreatorType)
    {
        ValidateUniqueEventConstraintCreatorType(uniqueEventConstraintCreatorType);

        eventTypes.Add(uniqueEventConstraintCreatorType.Name, uniqueEventConstraintCreatorType);
    }

    public static Type GetUniqueEventConstraintCreatorType(string name)
    {
        return eventTypes.TryGetValue(name, out var eventType)
            ? eventType
            : throw new EventNotRegisteredException(name);
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

        var UniqueEventConstraintCreatorTypeName = uniqueEventConstraintCreatorType.Name;

        if (eventTypes.TryGetValue(UniqueEventConstraintCreatorTypeName, out var registeredType))
        {
            if (registeredType == uniqueEventConstraintCreatorType)
                throw new InvalidOperationException(
                    $"Duplicate registration of uniqueEventConstraintCreatorType with name '{registeredType.FullName}'."
                );

            throw new InvalidOperationException(
                $"Unique Event ConstraintCreator name '{UniqueEventConstraintCreatorTypeName}' is already registered for "
                    + $"'{registeredType.FullName}'."
            );
        }
    }
}
