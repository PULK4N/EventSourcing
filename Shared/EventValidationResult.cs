#nullable enable

namespace EventSourcing.Shared.Models;

public class EventValidationResult
{
    public required string ValidatorName { get; set; }
    public required string EventName { get; set; }
    public required string State { get; set; }
    public required string StateMachineId { get; set; }
    public required uint OrderNumber { get; set; }
    public required Guid AggregateId { get; set; }

    public required bool Succeded { get; set; }
    public string? FailureReason { get; set; }

    public static EventValidationResult FromPayload(
        EventPayload payload,
        string validatorName,
        bool succeded = true,
        string? failureReason = null
    )
    {
        return new EventValidationResult()
        {
            ValidatorName = validatorName,
            EventName = payload.EventExecutionInfo.EventName,
            AggregateId = payload.EventExecutionInfo.AggregateId,
            OrderNumber = payload.EventExecutionInfo.OrderNumber,
            StateMachineId = payload.EventExecutionInfo.StateMachineId,
            State = payload.EventExecutionInfo.NewState,
            Succeded = succeded,
            FailureReason = failureReason
        };
    }
}
