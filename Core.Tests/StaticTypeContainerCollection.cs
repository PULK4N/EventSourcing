using Microsoft.Extensions.DependencyInjection;

namespace EventSourcing.Core.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StaticTypeContainerCollection : ICollectionFixture<StaticTypeContainerFixture>
{
    public const string Name = "Static type containers";
}

public sealed class StaticTypeContainerFixture
{
    public StaticTypeContainerFixture()
    {
        var services = new ServiceCollection();
        services.RegisterStateDataTypes();
        services.RegisterEventTypes();
        services.RegisterUniqueEventConstraintCreators();
    }
}
