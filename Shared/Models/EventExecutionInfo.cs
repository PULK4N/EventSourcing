namespace EventSourcing.Shared.Models
{
    public class EventExecutionInfo
    {
        public DateTime Timestamp { get; set; }
        public AggregateId AggregateId { get; set; }
        public EventExecutor EventExecutor { get; set; }
        public EventExecutor? OnBehalfOf { get; set; }
        public string EventName { get; set; }

        public uint OrderNumber { get; set; }
        public string StateMachineId { get; set; }
        public string NewState { get; set; }
    }
}
