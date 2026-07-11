using EventSourcing.Shared.Models;

namespace EventSourcing.Shared.Exceptions;

public class EventValidationException(List<EventValidationResult> validationResults)
    : Exception(CreateMessage(validationResults.AsReadOnly()))
{
    public IReadOnlyCollection<EventValidationResult> ValidationResults =>
        validationResults.AsReadOnly();

    private static string CreateMessage(IReadOnlyList<EventValidationResult> failures)
    {
        if (failures.Count == 0)
        {
            return "Event validation failed, but no failed validation results were provided.";
        }

        var failureList = failures.Select(
            (failure) =>
                $"ValidatorName: {failure.ValidatorName}."
                + Environment.NewLine
                + $"Order Number: {failure.OrderNumber}."
                + Environment.NewLine
                + $"Event: {failure.EventName}; "
                + Environment.NewLine
                + $"State machine: {failure.StateMachineId}; "
                + Environment.NewLine
                + $"State: {failure.State};"
                + Environment.NewLine
                + $"AggregateId: {failure.AggregateId}; Reason: "
                + Environment.NewLine
                + (failure.FailureReason ?? "No failure reason provided.")
        );

        return $"Event validation failed with {failures.Count} failure(s):"
            + Environment.NewLine
            + string.Join(
                Environment.NewLine + "------------------------------------" + Environment.NewLine,
                failureList
            );
    }
}
