using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Providers;
using EventSourcing.Persistence.Interfaces;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;
using Moq;
using Shared.Interfaces;

namespace EventSourcing.Core.Tests;

public class StateMachineHandlerUniqueConstraintTests
{
    private readonly Mock<IEventStore> _eventStore = new(MockBehavior.Strict);
    private readonly Mock<IUniqueEventConstraintProvider> _constraintProvider =
        new(MockBehavior.Strict);
    private readonly Mock<IEventValidatorProvider> _validatorProvider = new(MockBehavior.Strict);
    private readonly Mock<IStateDataProvider> _stateDataProvider = new(MockBehavior.Strict);
    private readonly StateMachineHandler _handler;

    public StateMachineHandlerUniqueConstraintTests()
    {
        _validatorProvider
            .Setup(provider => provider.GetPreEventStateValidators(It.IsAny<EventPayload>()))
            .ReturnsAsync(new List<IPreEventValidator>());
        _validatorProvider
            .Setup(provider => provider.GetPostEventStateValidators(It.IsAny<EventPayload>()))
            .ReturnsAsync(new List<IPostEventValidator>());
        _stateDataProvider
            .Setup(provider => provider.GetStateDataByStateMachine(It.IsAny<string>()))
            .ReturnsAsync(() => new TestStateData());

        _handler = new StateMachineHandler(
            _eventStore.Object,
            _validatorProvider.Object,
            _constraintProvider.Object,
            _stateDataProvider.Object,
            new OrderNumberHelper()
        );
    }

    [Fact]
    public async Task ExecuteEvents_UsesStateBeforeAndAfterEventForConstraints()
    {
        var aggregateId = Guid.NewGuid();
        var existingEvent = CreateEvent(aggregateId, "old@example.com", 1);
        var newEvent = CreateEvent(aggregateId, "new@example.com");
        _eventStore
            .Setup(store => store.GetEvents(It.IsAny<Guid[]>()))
            .ReturnsAsync(
                new Dictionary<Guid, EventPayload[]> { [aggregateId] =  [ existingEvent ] }
            );
        _eventStore
            .Setup(store => store.Write(It.IsAny<EventPayload[]>()))
            .Returns(Task.CompletedTask);

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

        await _handler.ExecuteEvents(newEvent);

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
        Assert.Equal(2u, newEvent.EventExecutionInfo.OrderNumber);
        _constraintProvider.Verify(
            provider => provider.GetConstraintsToRemove(It.IsAny<object>(), newEvent),
            Times.Once
        );
        _constraintProvider.Verify(
            provider => provider.GetConstraintsToAdd(It.IsAny<object>(), newEvent),
            Times.Once
        );
        _eventStore.Verify(
            store =>
                store.Write(
                    It.Is<EventPayload[]>(
                        payloads => payloads.Length == 1 && ReferenceEquals(payloads[0], newEvent)
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Calculate_DoesNotCreateConstraintsForHistoricalEvents()
    {
        var historicalEvent = CreateEvent(Guid.NewGuid(), "historical@example.com", 1);

        await _handler.Calculate([ historicalEvent ], [ ]);

        _constraintProvider.VerifyNoOtherCalls();
        Assert.Empty(historicalEvent.UniqueEventConstraintsToRemove);
        Assert.Empty(historicalEvent.UniqueEventConstraintsToAdd);
    }

    private static EventPayload CreateEvent(Guid aggregateId, string email, uint orderNumber = 0)
    {
        var payload = EventPayload.Create(
            Guid.NewGuid(),
            aggregateId,
            "users-state-machine",
            new SetEmailEvent(email)
        );
        payload.EventExecutionInfo.OrderNumber = orderNumber;
        return payload;
    }

    private sealed class TestStateData : ISharedStateData
    {
        public Guid Id { get; set; }
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
