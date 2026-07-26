using Shared.Interfaces;

namespace EventSourcing.Shared.Containers;

public static class EventValidatorContainer
{
    private static readonly Dictionary<string, IEventValidator> eventValidators =
        new(StringComparer.Ordinal);

    public static void AddEventValidator(Type eventValidatorType)
    {
        if (!typeof(IEventValidator).IsAssignableFrom(eventValidatorType))
            throw new ArgumentException(
                $"Type '{eventValidatorType.FullName}' does not implement "
                    + $"{nameof(IEventValidator)}.",
                nameof(eventValidatorType)
            );

        IEventValidator eventValidator;

        try
        {
            eventValidator =
                Activator.CreateInstance(eventValidatorType) as IEventValidator
                ?? throw new InvalidOperationException(
                    $"Could not create event validator '{eventValidatorType.FullName}'."
                );
        }
        catch (MissingMethodException exception)
        {
            throw new InvalidOperationException(
                $"Event validator '{eventValidatorType.FullName}' must have a public "
                    + "parameterless constructor.",
                exception
            );
        }

        eventValidators.Add(eventValidatorType.Name, eventValidator);
    }

    public static IEventValidator GetEventValidator(string name)
    {
        return eventValidators.TryGetValue(name, out var eventValidator)
            ? eventValidator
            : throw new InvalidOperationException(
                $"Event validator '{name}' is not registered."
            );
    }
}
