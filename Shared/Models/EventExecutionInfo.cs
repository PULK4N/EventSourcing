namespace EventSourcing.Shared.Models;

public class EventExecutionInfo
{
    public DateTime Timestamp { get; init; }
    public AggregateId AggregateId { get; init; }
    public EventExecutor EventExecutor { get; init; }
    public EventExecutor? OnBehalfOf { get; init; }
    public uint OrderNumber { get; set; }
    # nullable disable
    public string EventName { get; init; }
    public string StateMachineId { get; init; }
    public string NewState { get; init; }
    #nullable enable
}
