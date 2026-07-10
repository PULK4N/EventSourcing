namespace EventSourcing.Shared.Containers
{
    public static class EventTypeContainer
    {
        private static readonly Dictionary<string, Type> eventTypes =
            new Dictionary<string, Type>();

        public static void AddEventType(string fullName, Type localEvent)
        {
            var name = fullName.Split('.').Last();
            eventTypes.Add(name, localEvent);
        }

        public static Type? GetEventType(string name)
        {
            if (!eventTypes.ContainsKey(name))
                return null;
            return eventTypes[name];
        }
    }
}
