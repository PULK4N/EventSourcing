using EventSourcing.Core.Tests.TestModels;
using EventSourcing.Shared.Exceptions;
using EventSourcing.Shared.Models;
using Moq;
using Shared.Interfaces;

namespace EventSourcing.Core.Tests;

public sealed class StateValidatorTests
{
    private const string StateMachineId = "state-validator-tests";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ValidateEvent_ThrowsOnlyWhenValidationFails(bool succeeds)
    {
        var payload = CreatePayload();
        var stateData = new AccountStateData(payload.EventExecutionInfo.AggregateId);
        var validationResult = EventValidationResult.FromPayload(
            payload,
            "validator",
            succeeds,
            succeeds ? null : "Validation failed."
        );
        var validator = new Mock<IEventValidator>(MockBehavior.Strict);
        validator.Setup(x => x.Validate(stateData, payload)).Returns(validationResult);

        void Validate() => StateValidator.ValidateEvent(stateData, payload, [ validator.Object ]);

        if (succeeds)
        {
            Validate();
            return;
        }

        var exception = Assert.Throws<EventValidationException>(Validate);
        Assert.Same(validationResult, Assert.Single(exception.ValidationResults));
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(0u)]
    public void ValidateOldEventsContainOrderNumbers_ThrowsOnlyWhenOrderNumberIsMissing(
        uint orderNumber
    )
    {
        var payload = CreatePayload(orderNumber: orderNumber);
        var payload2 = CreatePayload(orderNumber: 2u);

        void Validate() =>
            StateValidator.ValidateOldEventsContainOrderNumbers([ payload, payload2 ]);

        var hasOrderNumber = orderNumber > 0;
        if (hasOrderNumber)
        {
            Validate();
            return;
        }

        var exception = Assert.Throws<EventValidationException>(Validate);
        var result = Assert.Single(exception.ValidationResults);
        Assert.Equal(
            nameof(StateValidator.ValidateOldEventsContainOrderNumbers),
            result.ValidatorName
        );
        Assert.Equal(Constants.INVALID_ORDER_NUMBER_ON_OLD_EVENT, result.FailureReason);
        Assert.Equal(payload.EventExecutionInfo.AggregateId, result.AggregateId);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(0u)]
    public void ValidateNewEventsOrderNumbers_ThrowsOnlyWhenOrderNumberIsSet(uint orderNumber)
    {
        var payload = CreatePayload(orderNumber: orderNumber);
        var payload2 = CreatePayload(orderNumber: 2u);

        void Validate() => StateValidator.ValidateNewEventsOrderNumbers([ payload ]);

        var hasOrderNumber = orderNumber > 0;
        if (!hasOrderNumber)
        {
            Validate();
            return;
        }

        var exception = Assert.Throws<EventValidationException>(Validate);
        var result = Assert.Single(exception.ValidationResults);
        Assert.Equal(nameof(StateValidator.ValidateNewEventsOrderNumbers), result.ValidatorName);
        Assert.Equal(Constants.INVALID_ORDER_NUMBER_ON_NEW_EVENT, result.FailureReason);
        Assert.Equal(payload.EventExecutionInfo.AggregateId, result.AggregateId);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ValidateAllEventsHaveSameAggregateIdAndStateMachineId_ValidatesIdentifiers(
        bool hasDifferentAggregateIds,
        bool hasDifferentStateMachineIds
    )
    {
        var aggregateIdPayload1 = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var firstPayload = CreatePayload(aggregateIdPayload1);

        var aggregateIdPayload2 = hasDifferentAggregateIds
            ? AggregateId.FromDatabaseGuid(Guid.NewGuid())
            : aggregateIdPayload1;
        var stateMachineIdPayload2 = hasDifferentStateMachineIds
            ? "another-state-machine"
            : StateMachineId;

        var secondPayload = CreatePayload(aggregateIdPayload2, stateMachineIdPayload2);

        void Validate() =>
            StateValidator.ValidateAllEventsHaveSameAggregateIdAndStateMachineId(
                [ firstPayload, secondPayload ],
                firstPayload
            );

        if (!hasDifferentAggregateIds && !hasDifferentStateMachineIds)
        {
            Validate();
            return;
        }

        var exception = Assert.Throws<EventValidationException>(Validate);
        var result = Assert.Single(exception.ValidationResults);
        Assert.Equal(
            nameof(StateValidator.ValidateAllEventsHaveSameAggregateIdAndStateMachineId),
            result.ValidatorName
        );
    }

    private static EventPayload CreatePayload(
        AggregateId? aggregateId = null,
        string stateMachineId = StateMachineId,
        uint orderNumber = 0
    )
    {
        var payload = EventPayload.Create(
            EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
            aggregateId ?? AggregateId.FromDatabaseGuid(Guid.NewGuid()),
            stateMachineId,
            new TransferMoney()
        );
        payload.EventExecutionInfo.OrderNumber = orderNumber;
        return payload;
    }
}
