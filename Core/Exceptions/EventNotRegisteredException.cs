namespace EventSourcing.Core
{
    public class EventNotRegisteredException : Exception
    {
        public EventNotRegisteredException(string eventName)
            : base(
                $"Class for the reducer named {eventName}, could not be instantiated. Possible error due to not registering dependency injection"
            ) { }
    }
}
