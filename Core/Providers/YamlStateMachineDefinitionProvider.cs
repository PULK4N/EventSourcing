using EventSourcing.Core.Interfaces;
using EventSourcing.Core.Models;
using EventSourcing.Shared.Exceptions;
using Microsoft.Extensions.Configuration;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace EventSourcing.Core.Providers;

public sealed class YamlStateMachineDefinitionProvider : IStateMachineDefinitionProvider
{
    public const string ConfigurationPath = "EventSourcing:StateMachinesPath";

    private readonly Dictionary<string, StateMachineDefinition> _definitions;

    public YamlStateMachineDefinitionProvider(IConfiguration configuration)
        : this(GetDirectoryPath(configuration)) { }

    public YamlStateMachineDefinitionProvider(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        _definitions = LoadDefinitions(Path.GetFullPath(directoryPath));
    }

    public StateMachineDefinition Get(string stateMachineId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateMachineId);

        return _definitions.TryGetValue(stateMachineId, out var definition)
            ? definition
            : throw new StateMachineNotRegisteredException(stateMachineId);
    }

    public IReadOnlyCollection<StateMachineDefinition> GetAll() => _definitions.Values;

    private static string GetDirectoryPath(IConfiguration configuration)
    {
        var configuredPath = configuration[ConfigurationPath];

        return string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(Directory.GetCurrentDirectory(), "StateMachines")
            : configuredPath;
    }

    private static Dictionary<string, StateMachineDefinition> LoadDefinitions(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException(
                $"State-machine directory '{directoryPath}' was not found."
            );

        var yamlFiles = Directory
            .EnumerateFiles(directoryPath, "*.yaml")
            .Concat(Directory.EnumerateFiles(directoryPath, "*.yml"))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (yamlFiles.Length == 0)
            throw new InvalidOperationException(
                $"State-machine directory '{directoryPath}' contains no YAML files."
            );

        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .WithDuplicateKeyChecking()
            .Build();
        var definitions = new Dictionary<string, StateMachineDefinition>(StringComparer.Ordinal);

        foreach (var yamlFile in yamlFiles)
        {
            StateMachineDefinition? definition;

            try
            {
                using var reader = File.OpenText(yamlFile);
                definition = deserializer.Deserialize<StateMachineDefinition>(reader);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Could not load state-machine file '{yamlFile}'.",
                    exception
                );
            }

            Validate(definition, yamlFile);

            if (!definitions.TryAdd(definition!.Id, definition))
                throw new InvalidOperationException(
                    $"State machine '{definition.Id}' is defined more than once."
                );
        }

        return definitions;
    }

    private static void Validate(StateMachineDefinition? definition, string yamlFile)
    {
        if (definition is null)
            throw new InvalidOperationException(
                $"State-machine file '{yamlFile}' contains no definition."
            );

        if (string.IsNullOrWhiteSpace(definition.Id))
            throw new InvalidOperationException(
                $"State-machine file '{yamlFile}' must define an id."
            );

        if (string.IsNullOrWhiteSpace(definition.StateData))
            throw new InvalidOperationException(
                $"State machine '{definition.Id}' must define stateData."
            );

        definition.Projections ??=  [ ];
        definition.Events ??=  [ ];
        ValidateIds(definition.Projections, $"state machine '{definition.Id}' projections");

        foreach (var (eventName, eventDefinition) in definition.Events)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                throw new InvalidOperationException(
                    $"State machine '{definition.Id}' contains an event without a name."
                );

            if (eventDefinition is null)
                throw new InvalidOperationException(
                    $"Event '{eventName}' in state machine '{definition.Id}' has no definition."
                );

            eventDefinition.UniqueConstraints ??=  [ ];
            eventDefinition.Projections ??=  [ ];
            ValidateIds(
                eventDefinition.UniqueConstraints,
                $"event '{eventName}' unique constraints"
            );
            ValidateIds(eventDefinition.Projections, $"event '{eventName}' projections");
        }
    }

    private static void ValidateIds(IEnumerable<string> ids, string location)
    {
        var encounteredIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var id in ids)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidOperationException($"{location} contains an empty ID.");

            if (!encounteredIds.Add(id))
                throw new InvalidOperationException($"{location} contains duplicate ID '{id}'.");
        }
    }
}
