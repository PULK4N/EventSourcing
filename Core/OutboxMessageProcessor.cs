using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Models;
using Microsoft.Extensions.Logging;

namespace EventSourcing.Core;

public sealed class OutboxMessageProcessor(
    IOutbox outbox,
    IEventStore eventStore,
    StateCalculator stateCalculator,
    ProjectionOutbox projectionOutbox,
    ILogger<OutboxMessageProcessor> logger
)
{
    public async Task<bool> ProcessNext(CancellationToken cancellationToken = default)
    {
        var message = await outbox.ReadNext(cancellationToken);
        if (message is null)
            return false;

        try
        {
            var payload = message.Payload;
            var aggregateId = payload.EventExecutionInfo.AggregateId;
            var targetOrderNumber = payload.EventExecutionInfo.OrderNumber;
            var histories = await eventStore.GetEvents([ aggregateId ]);

            if (!histories.TryGetValue(aggregateId, out var history))
                throw new InvalidOperationException(
                    $"No event history exists for outbox message {message.Id} and aggregate {aggregateId}."
                );

            if (
                !history.Any(
                    eventPayload =>
                        eventPayload.EventExecutionInfo.OrderNumber == targetOrderNumber
                        && eventPayload.EventExecutionInfo.StateMachineId
                            == payload.EventExecutionInfo.StateMachineId
                        && eventPayload.EventExecutionInfo.EventName
                            == payload.EventExecutionInfo.EventName
                )
            )
                throw new InvalidOperationException(
                    $"Event {targetOrderNumber} was not found for outbox message {message.Id} and aggregate {aggregateId}."
                );

            var historyThroughMessage = history
                .Where(
                    eventPayload =>
                        eventPayload.EventExecutionInfo.OrderNumber <= targetOrderNumber
                )
                .ToList();
            var stateInfo = await stateCalculator.Calculate(historyThroughMessage, [ ]);
            stateInfo.LastExecutedPayloads = [ payload ];

            await projectionOutbox.Write(
                new Dictionary<AggregateId, StateInfo> { [aggregateId] = stateInfo }
            );
            await outbox.UpdateCompleted(message.Id);

            return true;
        }
        catch (Exception exception)
        {
            await outbox.UpdateFailed(message.Id);

            if (message.ExecutionAttempts >= 3)
                logger.LogError(
                    exception,
                    "Outbox message {OutboxMessageId} failed its final projection attempt.",
                    message.Id
                );
            else
                logger.LogWarning(
                    exception,
                    "Outbox message {OutboxMessageId} failed projection attempt {Attempt}; it will be retried.",
                    message.Id,
                    message.ExecutionAttempts
                );

            return true;
        }
    }
}
