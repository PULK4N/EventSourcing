using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Providers;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;
using Moq;
using Shared.Interfaces;

namespace EventSourcing.Core.Tests;

public class StateCalculatorUniqueConstraintTests
{
    private readonly Mock<IUniqueEventConstraintProvider> _constraintProvider =
        new(MockBehavior.Strict);
    private readonly Mock<IEventValidatorProvider> _validatorProvider = new(MockBehavior.Strict);
    private readonly Mock<IStateDataProvider> _stateDataProvider = new(MockBehavior.Strict);
    private readonly StateCalculator _stateCalculator;

    public StateCalculatorUniqueConstraintTests()
    {
        _validatorProvider
            .Setup(provider => provider.GetPreEventStateValidators(It.IsAny<EventPayload>()))
            .ReturnsAsync(new List<IPreEventValidator>());
        _validatorProvider
            .Setup(provider => provider.GetPostEventStateValidators(It.IsAny<EventPayload>()))
            .ReturnsAsync(new List<IPostEventValidator>());
        _stateDataProvider
            .Setup(
                provider =>
                    provider.GetStateDataByStateMachine(
                        It.IsAny<string>(),
                        It.IsAny<AggregateId>()
                    )
            )
            .ReturnsAsync(
                (string _, AggregateId aggregateId) => new TestStateData(aggregateId)
            );

        _stateCalculator = new StateCalculator(
            new OrderNumberHelper(),
            _stateDataProvider.Object,
            _validatorProvider.Object,
            _constraintProvider.Object
        );
    }

    [Fact]
    public async Task Calculate_UsesStateBeforeAndAfterEventForConstraints()
    {
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var existingEvent = CreateEvent(aggregateId, "old@example.com", 1);
        var newEvent = CreateEvent(aggregateId, "new@example.com");

        # region Assert constraints to remove uses old state data and assert constraints to add uses new state data
        string? emailUsedForRemoval = null;
        string? emailUsedForAddition = null;
        _constraintProvider
            .Setup(provider => provider.GetConstraintsToRemove(It.IsAny<object>(), newEvent))
            .Returns(
                (object stateData, EventPayload _) =>
                {
                    var email = ((TestStateData)stateData).Email;
                    emailUsedForRemoval = email;
                    return email is null ? [ ] : [ new UniqueEventConstraintData("email", email) ];
                }
            );
        _constraintProvider
            .Setup(provider => provider.GetConstraintsToAdd(It.IsAny<object>(), newEvent))
            .Returns(
                (object stateData, EventPayload _) =>
                {
                    var email = ((TestStateData)stateData).Email;
                    emailUsedForAddition = email;
                    return email is null ? [ ] : [ new UniqueEventConstraintData("email", email) ];
                }
            );

        await _stateCalculator.Calculate([ existingEvent ], [ newEvent ]);

        Assert.Equal("old@example.com", emailUsedForRemoval);
        Assert.Equal("new@example.com", emailUsedForAddition);
        #endregion

        Assert.Equal(
            "old@example.com",
            Assert.Single(newEvent.UniqueEventConstraintsToRemove).ValueToHash
        );
        Assert.Equal(
            "new@example.com",
            Assert.Single(newEvent.UniqueEventConstraintsToAdd).ValueToHash
        );
        // Assert that orderNumber was set properly
        Assert.Equal(2u, newEvent.EventExecutionInfo.OrderNumber);
        // assert that the constraint provider was called once, because we have a single new event
        _constraintProvider.Verify(
            provider => provider.GetConstraintsToRemove(It.IsAny<object>(), newEvent),
            Times.Once
        );
        _constraintProvider.Verify(
            provider => provider.GetConstraintsToAdd(It.IsAny<object>(), newEvent),
            Times.Once
        );
        // Assert that the existing event doesn't have any constraints to remove or add, since it was already processed
        Assert.Empty(existingEvent.UniqueEventConstraintsToRemove);
        Assert.Empty(existingEvent.UniqueEventConstraintsToAdd);
    }

    private static EventPayload CreateEvent(
        AggregateId aggregateId,
        string email,
        uint orderNumber = 0
    )
    {
        var payload = EventPayload.Create(
            EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
            aggregateId,
            "users-state-machine",
            new SetEmailEvent(email)
        );
        payload.EventExecutionInfo.OrderNumber = orderNumber;
        return payload;
    }

    private sealed class TestStateData(AggregateId aggregateId) : ISharedStateData
    {
        public AggregateId Id { get; init; } = aggregateId;
        public bool IsDeleted { get; set; }
        public string? Email { get; set; }
    }

    private sealed record SetEmailEvent(string Email) : IEvent
    {
        public object Apply(object stateData, EventExecutionInfo eventExecutionInfo)
        {
            var state = (TestStateData)stateData;
            state.Email = Email;
            return state;
        }
    }
}
