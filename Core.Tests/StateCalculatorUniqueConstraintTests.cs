using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Models;
using EventSourcing.Core.Providers;
using EventSourcing.Core.Tests.TestModels.UniqueConstraints;
using EventSourcing.Shared.Models;
using Moq;

namespace EventSourcing.Core.Tests;

[Collection(StaticTypeContainerCollection.Name)]
public class StateCalculatorUniqueConstraintTests
{
    private const string StateMachineId = "users-state-machine";

    private readonly StateCalculator _stateCalculator;

    public StateCalculatorUniqueConstraintTests()
    {
        var stateDataProvider = new Mock<IStateDataProvider>();
        stateDataProvider
            .Setup(provider => provider.GetStateDataByStateMachine(StateMachineId, It.IsAny<AggregateId>()))
            .Returns((string _, AggregateId aggregateId) => new YamlUserStateData(aggregateId));
        var validatorProvider = new Mock<IEventValidatorProvider>();
        validatorProvider
            .Setup(provider => provider.GetPreEventStateValidators(It.IsAny<EventPayload>()))
            .Returns([ ]);
        validatorProvider
            .Setup(provider => provider.GetPostEventStateValidators(It.IsAny<EventPayload>()))
            .Returns([ ]);
        var definitions = new Mock<IStateMachineDefinitionProvider>();
        definitions
            .Setup(provider => provider.Get(StateMachineId))
            .Returns(
                new StateMachineDefinition
                {
                    Id = StateMachineId,
                    InitializationEvents = [ nameof(YamlUserUpdated) ]
                }
            );

        _stateCalculator = new StateCalculator(
            new OrderNumberHelper(),
            stateDataProvider.Object,
            validatorProvider.Object,
            definitions.Object
        );
    }

    // Expected constraints: one value is an email constraint, two values are username + name.
    // Changed compares the final constraints with the ones after the existing events only.
    public static TheoryData<int, List<YamlUserUpdated>, List<string[]>, bool> Cases =>
        new()
        {
            { 0, [ new(null) ], [ ], false },
            { 0, [ new("a@x.com") ], [ [ "a@x.com" ] ], true },
            { 0, [ new("a@x.com", "john", "John Smith") ], [ [ "a@x.com" ], [ "john", "John Smith" ] ], true },
            // Only the final state counts, not the values set in between.
            { 0, [ new("a@x.com"), new("b@x.com") ], [ [ "b@x.com" ] ], true },
            { 0, [ new("a@x.com"), new(null), new("a@x.com") ], [ [ "a@x.com" ] ], true },
            // A deleted state holds no values.
            { 0, [ new("a@x.com", "john", "John Smith", IsDeleted: true) ], [ ], false },
            // Existing events alone still produce the current constraints.
            { 1, [ new("a@x.com") ], [ [ "a@x.com" ] ], false },
            { 1, [ new("a@x.com"), new("b@x.com") ], [ [ "b@x.com" ] ], true },
            { 1, [ new("a@x.com"), new("a@x.com") ], [ [ "a@x.com" ] ], false },
            { 1, [ new("a@x.com"), new("b@x.com"), new("a@x.com") ], [ [ "a@x.com" ] ], false },
            { 1, [ new("a@x.com"), new(null) ], [ ], true },
            { 1, [ new("a@x.com", "john", "John Smith"), new("a@x.com", "john", "John Doe") ], [ [ "a@x.com" ], [ "john", "John Doe" ] ], true },
            { 1, [ new("a@x.com"), new("a@x.com", IsDeleted: true) ], [ ], true },
        };

    [Theory]
    [MemberData(nameof(Cases))]
    public void CreatesConstraintsFromFinalState(
        int existingCount,
        List<YamlUserUpdated> events,
        List<string[]> expectedConstraints,
        bool expectedChanged
    )
    {
        var aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
        var payloads = events
            .Select(
                (eventData, index) =>
                {
                    var payload = EventPayload.Create(
                        EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
                        aggregateId,
                        StateMachineId,
                        eventData
                    );
                    if (index < existingCount)
                        payload.EventExecutionInfo.OrderNumber = (uint)index + 1;
                    return payload;
                }
            )
            .ToList();

        var stateInfo = _stateCalculator.Calculate(
            payloads.Take(existingCount).ToList(),
            payloads.Skip(existingCount).ToList()
        );

        var expectedState = new YamlUserStateData(aggregateId);
        var expected = expectedConstraints.Select(
            values =>
                values.Length == 1
                    ? (UniqueConstraint)new UniqueUserEmailConstraint(expectedState, values[0])
                    : new UniqueUsernameAndNameConstraint(expectedState, values[0], values[1])
        );

        Assert.Equal(
            expected.Select(Describe).Order(),
            stateInfo.UniqueConstraints.Select(Describe).Order()
        );
        Assert.Equal(expectedChanged, stateInfo.UniqueConstraintsChanged);
    }

    private static string Describe(UniqueConstraint constraint) =>
        $"{constraint.AggregateId}:{constraint.ConstraintName}:{Convert.ToHexString(constraint.Hash)}";
}
