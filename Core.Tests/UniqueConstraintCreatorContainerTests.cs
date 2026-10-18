using System.Reflection;
using EventSourcing.Core.Tests.TestModels;
using EventSourcing.Core.Tests.TestModels.UniqueConstraints;
using EventSourcing.Shared.Containers;
using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Tests;

[Collection(StaticTypeContainerCollection.Name)]
public class UniqueConstraintCreatorContainerTests
{
    [Theory]
    [InlineData(
        typeof(YamlUserStateData),
        new[] { nameof(UserEmailConstraintCreator), nameof(UsernameAndNameConstraintCreator) }
    )]
    [InlineData(typeof(AccountStateData), new string[0])]
    public void RegistersCreatorsPerStateData(Type stateDataType, string[] expectedNames)
    {
        var creators = GetRegisteredCreators(stateDataType);

        Assert.Equal(
            expectedNames.Order(),
            creators.Select(creator => creator.GetType().Name).Order()
        );
        Assert.All(creators, creator => Assert.Equal(stateDataType, creator.StateDataType));
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("user@example.com", 1)]
    public void RegisteredCreatorCreatesConstraintsFromStateData(string? email, int expectedCount)
    {
        var creator = Assert.Single(
            GetRegisteredCreators(typeof(YamlUserStateData)),
            creator => creator is UserEmailConstraintCreator
        );
        var stateData = new YamlUserStateData(AggregateId.FromDatabaseGuid(Guid.NewGuid()))
        {
            Email = email
        };

        var constraints = creator.Create(stateData);

        Assert.Equal(expectedCount, constraints.Count);
        Assert.All(
            constraints,
            constraint =>
            {
                Assert.IsType<UniqueUserEmailConstraint>(constraint);
                Assert.Equal(new UniqueUserEmailConstraint(stateData, email!).Hash, constraint.Hash);
            }
        );
    }

    private static List<IUniqueConstraintCreator> GetRegisteredCreators(Type stateDataType)
    {
        var field = typeof(UniqueConstraintCreatorContainer).GetField(
            "creatorsByStateData",
            BindingFlags.NonPublic | BindingFlags.Static
        )!;
        var creatorsByStateData = (Dictionary<Type, List<IUniqueConstraintCreator>>)
            field.GetValue(null)!;

        return creatorsByStateData.GetValueOrDefault(stateDataType) ?? [ ];
    }
}
