using EventSourcing.Core;
using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Models;
using EventSourcing.Core.Providers;
using EventSourcing.Shared.Models;
using Moq;

namespace EventSourcing.Optimizations.Tests.TestModels;

/// <summary>
/// Builds a real <see cref="StateCalculator"/> and payloads for a counter state machine
/// without validators or unique-constraint creators.
/// </summary>
public static class CounterStateMachine
{
    public const string Id = "optimizations-tests";

    public static StateCalculator CreateCalculator()
    {
        var stateDataProvider = new Mock<IStateDataProvider>();
        stateDataProvider
            .Setup(provider => provider.GetStateDataByStateMachine(Id, It.IsAny<AggregateId>()))
            .Returns((string _, AggregateId aggregateId) => new CounterStateData(aggregateId));

        var validatorProvider = new Mock<IEventValidatorProvider>();
        validatorProvider
            .Setup(provider => provider.GetPreEventStateValidators(It.IsAny<EventPayload>()))
            .Returns([ ]);
        validatorProvider
            .Setup(provider => provider.GetPostEventStateValidators(It.IsAny<EventPayload>()))
            .Returns([ ]);

        var definitionProvider = new Mock<IStateMachineDefinitionProvider>();
        definitionProvider
            .Setup(provider => provider.Get(Id))
            .Returns(
                new StateMachineDefinition
                {
                    Id = Id,
                    InitializationEvents = [ nameof(AddToCounter) ]
                }
            );

        return new StateCalculator(
            new OrderNumberHelper(),
            stateDataProvider.Object,
            validatorProvider.Object,
            definitionProvider.Object
        );
    }

    /// <param name="orderNumber">Set for stored events; leave unset for events to execute.</param>
    public static EventPayload Payload(
        AggregateId aggregateId,
        int amount,
        uint orderNumber = EventExecutionInfo.UNSET_ORDER_NUMBER
    )
    {
        var payload = EventPayload.Create(
            EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
            aggregateId,
            Id,
            new AddToCounter { Amount = amount }
        );
        payload.EventExecutionInfo.OrderNumber = orderNumber;
        return payload;
    }
}
