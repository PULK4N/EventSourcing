namespace EventSourcing.Core.Models;

public sealed class StateMachineDefinition
{
    public string Id { get; set; } = string.Empty;
    public string StateData { get; set; } = string.Empty;
    public HashSet<string> InitializationEvents { get; set; } = new(StringComparer.Ordinal);
    public List<string> Projections { get; set; } = [ ];
    public Dictionary<string, StateMachineEventDefinition> Events { get; set; } = [ ];
}

public sealed class StateMachineEventDefinition
{
    public HashSet<string> Hooks { get; set; } = new(StringComparer.Ordinal);
    public List<string> PreEventValidators { get; set; } = [ ];
    public List<string> PostEventValidators { get; set; } = [ ];
    public List<string> Projections { get; set; } = [ ];
}
