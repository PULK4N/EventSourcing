using EventSourcing.Shared.Exceptions;

namespace EventSourcing.Shared.Containers;

public static class EventTypeContainer
{
    private static readonly Dictionary<string, Type> eventTypes = new Dictionary<string, Type>();

    public static void AddEventType(string fullName, Type @Event)
    {
        eventTypes.Add(fullName, @Event);
    }

    public static Type GetEventType(string name)
    {
        if (!eventTypes.ContainsKey(name))
            throw new EventNotRegisteredException(name);
        return eventTypes[name];
    }
}
