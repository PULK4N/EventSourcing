namespace EventSourcing.Shared.Exceptions;

public class EventNotRegisteredException : Exception
{
    public EventNotRegisteredException(string eventName)
        : base(
            $"Class for the IEvent named {eventName}, could not be instantiated. Possible error due to not registering dependency injection"
        ) { }
}
