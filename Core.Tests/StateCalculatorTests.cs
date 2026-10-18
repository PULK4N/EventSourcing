using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Models;
using EventSourcing.Core.Providers;
using EventSourcing.Core.Tests.TestModels;
using EventSourcing.Shared.Exceptions;
using EventSourcing.Shared.Models;
using Moq;

namespace EventSourcing.Core.Tests;

public sealed class StateCalculatorTests
{
    private const string StateMachineId = "state-calculator-tests";
    private readonly StateCalculator _stateCalculator;

    public StateCalculatorTests()
    {
        var stateDataProvider = new Mock<IStateDataProvider>();
        stateDataProvider
            .Setup(
                provider =>
                    provider.GetStateDataByStateMachine(
                        StateMachineId,
                        It.IsAny<AggregateId>()
                    )
            )
            .Returns(
                (string _, AggregateId aggregateId) =>
                    new AccountStateData(aggregateId) { Money = 100 }
            );
        var validatorProvider = new Mock<IEventValidatorProvider>();
        validatorProvider
            .Setup(provider => provider.GetPreEventStateValidators(It.IsAny<EventPayload>()))
            .Returns([ ]);
        validatorProvider
            .Setup(provider => provider.GetPostEventStateValidators(It.IsAny<EventPayload>()))
            .Returns([ ]);
        var stateMachineDefinitionsProvider = new Mock<IStateMachineDefinitionProvider>();
        stateMachineDefinitionsProvider
            .Setup(provider => provider.Get(StateMachineId))
            .Returns(
                new StateMachineDefinition
                {
                    Id = StateMachineId,
                    InitializationEvents = [ nameof(TransferMoney) ]
                }
            );
        _stateCalculator = new StateCalculator(
            new OrderNumberHelper(),
            stateDataProvider.Object,
            validatorProvider.Object,
            stateMachineDefinitionsProvider.Object
        );
    }

    [Fact]
    public void Calculate_RequiresInitializationEventForNewAggregate()
    {
        var payload = EventPayload.Create(
            EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
            AggregateId.FromDatabaseGuid(Guid.NewGuid()),
            StateMachineId,
            new MultiplyMoney { Multiplier = 2 }
        );

        var exception = Assert.Throws<EventValidationException>(
            () => _stateCalculator.Calculate([ ], [ payload ])
        );

        Assert.Equal(
            Constants.INITIALIZATION_EVENT_REQUIRED,
            Assert.Single(exception.ValidationResults).FailureReason
        );
    }

    [Fact]
    public void Calculate_ReturnsCorrectStateInfo()
    {
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var existingPayload = EventPayload.Create(
            EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
            aggregateId,
            StateMachineId,
            new TransferMoney { MoneySent = 10 }
        );
        existingPayload.EventExecutionInfo.OrderNumber = 1;
        var lastPayload = EventPayload.Create(
            EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
            aggregateId,
            StateMachineId,
            new TransferMoney { MoneySent = 20 }
        );

        var stateInfo = _stateCalculator.Calculate([ existingPayload ], [ lastPayload ]);

        Assert.Equal(2u, stateInfo.CurrentOrderNumber);
        Assert.Equal(lastPayload.EventExecutionInfo.Timestamp, stateInfo.LastUpdateTimestamp);
        Assert.Equal(aggregateId, stateInfo.AggregateId);
        Assert.Equal(StateMachineId, stateInfo.StateMachineId);
        var stateData = Assert.IsType<AccountStateData>(stateInfo.StateData);
        Assert.Equal(aggregateId, stateData.Id);
        Assert.Equal(70, stateData.Money);
    }

    [Fact]
    public void Calculate_ThrowsForMixedAggregateIds()
    {
        var existingPayload = EventPayload.Create(
            EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
            AggregateId.FromDatabaseGuid(Guid.NewGuid()),
            StateMachineId,
            new TransferMoney { MoneySent = 10 }
        );
        existingPayload.EventExecutionInfo.OrderNumber = 1;

        var newPayload = EventPayload.Create(
            EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
            AggregateId.FromDatabaseGuid(Guid.NewGuid()),
            StateMachineId,
            new TransferMoney { MoneySent = 20 }
        );

        Assert.Throws<EventValidationException>(
            () => _stateCalculator.Calculate([ existingPayload ], [ newPayload ])
        );
    }

    [Fact]
    public void Calculate_ThrowsForMixedStateMachineIds()
    {
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var existingPayload = EventPayload.Create(
            EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
            aggregateId,
            StateMachineId,
            new TransferMoney { MoneySent = 10 }
        );
        existingPayload.EventExecutionInfo.OrderNumber = 1;

        var newPayload = EventPayload.Create(
            EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
            aggregateId,
            "another-state-machine",
            new TransferMoney { MoneySent = 20 }
        );

        Assert.Throws<EventValidationException>(
            () => _stateCalculator.Calculate([ existingPayload ], [ newPayload ])
        );
    }

    [Fact]
    public void Calculate_ExecutesExistingEventsByOrderNumber()
    {
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var transferPayload = EventPayload.Create(
            EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
            aggregateId,
            StateMachineId,
            new TransferMoney { MoneySent = 10 }
        );
        transferPayload.EventExecutionInfo.OrderNumber = 1;
        var multiplyPayload = EventPayload.Create(
            EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
            aggregateId,
            StateMachineId,
            new MultiplyMoney { Multiplier = 2 }
        );
        multiplyPayload.EventExecutionInfo.OrderNumber = 2;

        var stateInfo = _stateCalculator.Calculate(
            [ multiplyPayload, transferPayload ],
            [ ]
        );

        var stateData = Assert.IsType<AccountStateData>(stateInfo.StateData);
        Assert.Equal(180, stateData.Money);
    }
}
