namespace EventSourcing.Shared.Models
{
    public class EventExecutionInfo
    {
        public DateTime Timestamp { get; set; }
        public Guid AggregateId { get; set; }
        public Guid EventExecutor { get; set; }
        public string EventName { get; set; }

        public string AssemblyQualifiedEventName { get; set; }
        public Guid Id { get; set; }
        public uint OrderNumber { get; set; }
        public string StateMachineId { get; set; }
        public string NewState { get; set; }
    }
}
