using EventSourcing.Core.Interfaces;
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
            .ReturnsAsync(
                (string _, AggregateId aggregateId) =>
                    new AccountStateData(aggregateId) { Money = 100 }
            );
        var validatorProvider = new Mock<IEventValidatorProvider>();
        validatorProvider
            .Setup(provider => provider.GetPreEventStateValidators(It.IsAny<EventPayload>()))
            .ReturnsAsync([ ]);
        validatorProvider
            .Setup(provider => provider.GetPostEventStateValidators(It.IsAny<EventPayload>()))
            .ReturnsAsync([ ]);
        var constraintProvider = new Mock<IUniqueEventConstraintProvider>();
        constraintProvider
            .Setup(
                provider =>
                    provider.GetConstraintsToRemove(
                        It.IsAny<object>(),
                        It.IsAny<EventPayload>()
                    )
            )
            .Returns([ ]);
        constraintProvider
            .Setup(
                provider =>
                    provider.GetConstraintsToAdd(
                        It.IsAny<object>(),
                        It.IsAny<EventPayload>()
                    )
            )
            .Returns([ ]);
        _stateCalculator = new StateCalculator(
            new OrderNumberHelper(),
            stateDataProvider.Object,
            validatorProvider.Object,
            constraintProvider.Object
        );
    }

    [Fact]
    public async Task Calculate_ReturnsCorrectStateInfo()
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

        var stateInfo = await _stateCalculator.Calculate([ existingPayload ], [ lastPayload ]);

        Assert.Equal(2u, stateInfo.CurrentOrderNumber);
        Assert.Equal(lastPayload.EventExecutionInfo.Timestamp, stateInfo.LastUpdateTimestamp);
        Assert.Equal(aggregateId, stateInfo.AggregateId);
        Assert.Equal(StateMachineId, stateInfo.StateMachineId);
        var stateData = Assert.IsType<AccountStateData>(stateInfo.StateData);
        Assert.Equal(aggregateId, stateData.Id);
        Assert.Equal(70, stateData.Money);
    }

    [Fact]
    public async Task Calculate_ThrowsForMixedAggregateIds()
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

        await Assert.ThrowsAsync<EventValidationException>(
            () => _stateCalculator.Calculate([ existingPayload ], [ newPayload ])
        );
    }

    [Fact]
    public async Task Calculate_ThrowsForMixedStateMachineIds()
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

        await Assert.ThrowsAsync<EventValidationException>(
            () => _stateCalculator.Calculate([ existingPayload ], [ newPayload ])
        );
    }

    [Fact]
    public async Task Calculate_ExecutesExistingEventsByOrderNumber()
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

        var stateInfo = await _stateCalculator.Calculate(
            [ multiplyPayload, transferPayload ],
            [ ]
        );

        var stateData = Assert.IsType<AccountStateData>(stateInfo.StateData);
        Assert.Equal(180, stateData.Money);
    }
}
