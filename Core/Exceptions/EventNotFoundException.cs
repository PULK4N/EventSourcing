namespace EventSourcing.Core
{
    public class EventNotFoundException : Exception
    {
        public EventNotFoundException(string eventName)
            : base(
                $"Class for the reducer named {eventName}, could not be found. Possible error due to assembly configuration"
            ) { }
    }
}
