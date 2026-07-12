using EventSourcing.Core.Providers;

namespace EventSourcing.Core.Tests;

public class YamlStateMachineDefinitionProviderTests
{
    [Fact]
    public void LoadsStateMachineDefinitionFromYaml()
    {
        var directoryPath = CreateTemporaryDirectory();

        try
        {
            File.WriteAllText(
                Path.Combine(directoryPath, "users.yaml"),
                """
                id: users-state-machine
                stateData: UserStateData
                projections:
                  - user-audit
                events:
                  UserCreated:
                    uniqueConstraints:
                      - unique-email
                      - unique-username
                    projections:
                      - user-search
                """
            );

            var provider = new YamlStateMachineDefinitionProvider(directoryPath);

            var definition = provider.Get("users-state-machine");
            Assert.Equal("UserStateData", definition.StateData);
            Assert.Equal([ "user-audit" ], definition.Projections);
            var userCreated = definition.Events["UserCreated"];
            Assert.Equal([ "unique-email", "unique-username" ], userCreated.UniqueConstraints);
            Assert.Equal([ "user-search" ], userCreated.Projections);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    [Fact]
    public void RejectsDuplicateStateMachineIds()
    {
        var directoryPath = CreateTemporaryDirectory();

        try
        {
            const string yaml = """
                id: users-state-machine
                stateData: UserStateData
                """;
            File.WriteAllText(Path.Combine(directoryPath, "first.yaml"), yaml);
            File.WriteAllText(Path.Combine(directoryPath, "second.yml"), yaml);

            var exception = Assert.Throws<InvalidOperationException>(() =>
                new YamlStateMachineDefinitionProvider(directoryPath)
            );

            Assert.Contains("defined more than once", exception.Message);
        }
        finally
        {
            Directory.Delete(directoryPath, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            $"event-sourcing-tests-{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }
}
