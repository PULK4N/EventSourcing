using EventSourcing.Persistence;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Tests;

public class UniqueEventConstraintValidatorTests
{
    private const string ConstraintName = "email";
    private const string ConstraintValue = "user@example.com";

    private readonly Dictionary<string, AggregateId> _aggregates = new();

    public enum Operation
    {
        Add,
        Remove
    }

    [Theory]
    [InlineData(Operation.Add, "A", Operation.Add, "A", true)]
    [InlineData(Operation.Add, "A", Operation.Add, "B", true)]
    [InlineData(Operation.Remove, "A", Operation.Remove, "A", true)]
    [InlineData(Operation.Remove, "A", Operation.Remove, "B", true)]
    [InlineData(Operation.Add, "A", Operation.Remove, "A", true)]
    [InlineData(Operation.Add, "A", Operation.Remove, "B", false)]
    public void Validate_SameConstraintInOneWrite(
        Operation firstOperation,
        string firstAggregate,
        Operation secondOperation,
        string secondAggregate,
        bool shouldThrow
    )
    {
        var payloads = new List<EventPayload>
        {
            CreatePayload(firstAggregate, firstOperation),
            CreatePayload(secondAggregate, secondOperation)
        };

        var act = () =>
            UniqueEventConstraintValidator.ValidateConditionIsNotAddedThenRemovedInTheSameWrite(
                payloads
            );

        if (shouldThrow)
            Assert.Throws<InvalidOperationException>(act);
        else
            act();
    }

    private EventPayload CreatePayload(string aggregateKey, Operation operation)
    {
        if (!_aggregates.TryGetValue(aggregateKey, out var aggregateId))
        {
            aggregateId = AggregateId.FromDatabaseGuid(Guid.NewGuid());
            _aggregates[aggregateKey] = aggregateId;
        }

        var payload = EventPayload.Create(
            EventExecutor.FromDatabaseGuid(Guid.NewGuid()),
            aggregateId,
            "users-state-machine",
            new NoOpEvent()
        );

        var constraint = new UniqueEventConstraintData(ConstraintName, ConstraintValue);
        var target =
            operation == Operation.Add
                ? payload.UniqueEventConstraintsToAdd
                : payload.UniqueEventConstraintsToRemove;
        target.Add(constraint);

        return payload;
    }

    private sealed record NoOpEvent : IEvent
    {
        public object Apply(object stateData, EventExecutionInfo eventExecutionInfo) =>
            stateData;
    }
}
