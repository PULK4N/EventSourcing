using EventSourcing.Shared.Interfaces;

namespace EventSourcing.Core.Tests.TestModels.UniqueConstraints;

public sealed class UsernameAndNameConstraintCreator
    : IUniqueConstraintCreator<YamlUserStateData, UniqueUsernameAndNameConstraint>
{
    public List<UniqueUsernameAndNameConstraint> Create(YamlUserStateData stateData) =>
        stateData.Username is null || stateData.Name is null
            ? [ ]
            : [ new UniqueUsernameAndNameConstraint(stateData, stateData.Username, stateData.Name) ];
}
