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
        var testAssembly = typeof(StaticTypeContainerFixture).Assembly;
        services.RegisterStateDataTypes(testAssembly);
        services.RegisterEventTypes(testAssembly);
        services.RegisterUniqueEventConstraintCreators(testAssembly);
        services.RegisterEventValidators(testAssembly);
    }
}
