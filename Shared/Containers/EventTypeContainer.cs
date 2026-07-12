using EventSourcing.Shared.Exceptions;
using EventSourcing.Shared.Interfaces;

namespace EventSourcing.Shared.Containers;

public static class EventTypeContainer
{
    private static readonly Dictionary<string, Type> eventTypes = new(StringComparer.Ordinal);

    public static void AddEventType(Type eventType)
    {
        ValidateEventType(eventType);

        eventTypes.Add(eventType.Name, eventType);
    }

    public static Type GetEventType(string name)
    {
        return eventTypes.TryGetValue(name, out var eventType)
            ? eventType
            : throw new EventNotRegisteredException(name);
    }

    private static void ValidateEventType(Type eventType)
    {
        if (!typeof(IEvent).IsAssignableFrom(eventType))
            throw new ArgumentException(
                $"Type '{eventType.FullName}' does not implement {nameof(IEvent)}.",
                nameof(eventType)
            );

        var eventName = eventType.Name;

        if (eventTypes.TryGetValue(eventName, out var registeredType))
        {
            if (registeredType == eventType)
                throw new InvalidOperationException(
                    $"Duplicate registration of event with name '{registeredType.FullName}'."
                );

            throw new InvalidOperationException(
                $"Event name '{eventName}' is already registered for "
                    + $"'{registeredType.FullName}'."
            );
        }
    }
}
