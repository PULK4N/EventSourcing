using EventSourcing.Shared.Interfaces;

namespace EventSourcing.Core.Tests.TestModels.UniqueConstraints;

public sealed class UserEmailConstraintCreator
    : IUniqueConstraintCreator<YamlUserStateData, UniqueUserEmailConstraint>
{
    public List<UniqueUserEmailConstraint> Create(YamlUserStateData stateData) =>
        stateData.Email is null ? [ ] : [ new UniqueUserEmailConstraint(stateData, stateData.Email) ];
}
