using EventSourcing.Core.Tests.TestModels;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Tests;

public class UniqueConstraintTests
{
    [Fact]
    public void TakesNameFromTypeAndAggregateFromStateData()
    {
        var stateData = new YamlUserStateData(AggregateId.FromDatabaseGuid(Guid.NewGuid()));

        var constraint = new FirstConstraint(stateData, "user@example.com");

        Assert.Equal(nameof(FirstConstraint), constraint.ConstraintName);
        Assert.Equal(stateData.Id.Value, constraint.AggregateId);
        Assert.Equal(32, constraint.Hash.Length);
    }

    [Theory]
    // Same state-data type, constraint, and value hash the same even for different aggregates.
    [InlineData(typeof(YamlUserStateData), false, "a", typeof(YamlUserStateData), false, "a", true)]
    [InlineData(typeof(YamlUserStateData), false, "a", typeof(YamlUserStateData), false, "b", false)]
    [InlineData(typeof(YamlUserStateData), false, "a", typeof(AccountStateData), false, "a", false)]
    [InlineData(typeof(YamlUserStateData), false, "a", typeof(YamlUserStateData), true, "a", false)]
    public void HashIdentifiesStateDataTypeConstraintAndValue(
        Type firstStateDataType,
        bool firstIsSecondConstraint,
        string firstValue,
        Type secondStateDataType,
        bool secondIsSecondConstraint,
        string secondValue,
        bool expectedEqual
    )
    {
        var first = Create(firstStateDataType, firstIsSecondConstraint, firstValue);
        var second = Create(secondStateDataType, secondIsSecondConstraint, secondValue);

        Assert.Equal(expectedEqual, first.Hash.SequenceEqual(second.Hash));
    }

    private static UniqueConstraint Create(Type stateDataType, bool secondConstraint, string value)
    {
        var stateData = (ISharedStateData)Activator.CreateInstance(
            stateDataType,
            AggregateId.FromDatabaseGuid(Guid.NewGuid())
        )!;

        return secondConstraint
            ? new SecondConstraint(stateData, value)
            : new FirstConstraint(stateData, value);
    }

    private sealed class FirstConstraint(ISharedStateData stateData, string value)
        : UniqueConstraint(stateData, value);

    private sealed class SecondConstraint(ISharedStateData stateData, string value)
        : UniqueConstraint(stateData, value);
}
