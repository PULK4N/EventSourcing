namespace EventSourcing.Shared.Models
{
    /*
     * State info for a given aggregate
     * Make sure to instantiate only in State Handlers
     */
    public class EventValidationResult
    {
        public string Id { get; set; }
        public string EventName { get; set; }
        public string State { get; set; }
        public string StateMachineId { get; set; }
        public Guid AggregateId { get; set; }
    }
}
