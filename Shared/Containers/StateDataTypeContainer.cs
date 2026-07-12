using EventSourcing.Shared.Exceptions;
using EventSourcing.Shared.Models;

namespace EventSourcing.Shared.Containers;

public static class StateDataTypeContainer
{
    private static readonly Dictionary<string, Type> stateDataTypes =
        new Dictionary<string, Type>();

    public static void AddStateDataType(Type stateData)
    {
        ValidateThatStateDataNameIsUnique(stateData, stateData.Name);

        stateDataTypes.Add(stateData.Name, stateData);
    }

    public static Type GetStateDataType(string name)
    {
        if (!stateDataTypes.ContainsKey(name))
            throw new StateDataTypeNotFoundException(name);
        return stateDataTypes[name];
    }

    private static bool ValidateThatStateDataNameIsUnique(Type stateDataType, string name)
    {
        if (!typeof(ISharedStateData).IsAssignableFrom(stateDataType))
            throw new ArgumentException(
                $"Type '{stateDataType.FullName}' does not implement {nameof(ISharedStateData)}.",
                nameof(stateDataType)
            );

        if (stateDataTypes.TryGetValue(name, out var registeredType))
        {
            if (registeredType == stateDataType)
                throw new InvalidOperationException(
                    $"Duplicate registration of state data with name '{stateDataType.FullName}'."
                );

            throw new InvalidOperationException(
                $"Multiple state-data types are registered with the name '{name}'."
            );
        }

        return true;
    }
}
