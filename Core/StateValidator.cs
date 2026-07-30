using EventSourcing.Shared.Exceptions;
using EventSourcing.Shared.Models;
using Shared.Interfaces;

namespace EventSourcing.Core;

internal static class StateValidator
{
    internal static void ValidateEvent(
        object stateData,
        EventPayload payload,
        IEnumerable<IEventValidator> validators
    )
    {
        var validationResults = validators
            .Select(validator => validator.Validate(stateData, payload))
            .ToList();

        var failedValidations = validationResults.Where(x => x.Succeded == false).ToList();

        if (!failedValidations.Any())
            return;

        throw new EventValidationException(failedValidations);
    }

    internal static void ValidateOldEventsContainOrderNumbers(List<EventPayload> payloads)
    {
        var payloadsWithNoOrderNumber = payloads
            .Where(x => x.EventExecutionInfo.OrderNumber <= 0)
            .ToList();

        if (payloadsWithNoOrderNumber.Count > 0)
            throw new EventValidationException(
                payloadsWithNoOrderNumber
                    .Select(
                        payload =>
                            EventValidationResult.FromPayload(
                                payload,
                                nameof(ValidateOldEventsContainOrderNumbers),
                                false,
                                Constants.INVALID_ORDER_NUMBER_ON_OLD_EVENT
                            )
                    )
                    .ToList()
            );
    }

    internal static void ValidateNewEventsOrderNumbers(List<EventPayload> payloads)
    {
        var payloadsWithOrderNumber = payloads
            .Where(x => x.EventExecutionInfo.OrderNumber != 0)
            .ToList();

        if (payloadsWithOrderNumber.Count > 0)
            throw new EventValidationException(
                payloadsWithOrderNumber
                    .Select(
                        payload =>
                            EventValidationResult.FromPayload(
                                payload,
                                nameof(ValidateNewEventsOrderNumbers),
                                false,
                                Constants.INVALID_ORDER_NUMBER_ON_NEW_EVENT
                            )
                    )
                    .ToList()
            );
    }

    internal static void ValidateAllEventsHaveSameAggregateIdAndStateMachineId(
        List<EventPayload> allPayloads,
        EventPayload firstPayload
    )
    {
        var aggregateId = firstPayload.EventExecutionInfo.AggregateId;
        var stateMachineId = firstPayload.EventExecutionInfo.StateMachineId;
        var invalidPayloads = allPayloads
            .Where(
                x =>
                    x.EventExecutionInfo.AggregateId != aggregateId
                    || x.EventExecutionInfo.StateMachineId != stateMachineId
            )
            .ToList();

        var message = string.Format(
            Constants.DIFFERENT_STATE_MACHINE_ID_OR_AGGREGATE_ID,
            aggregateId,
            stateMachineId
        );

        if (invalidPayloads.Count > 0)
            throw new EventValidationException(
                invalidPayloads
                    .Select(
                        payload =>
                            EventValidationResult.FromPayload(
                                payload,
                                nameof(ValidateAllEventsHaveSameAggregateIdAndStateMachineId),
                                false,
                                message
                            )
                    )
                    .ToList()
            );
    }
}
