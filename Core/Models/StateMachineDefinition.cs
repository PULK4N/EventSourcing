namespace EventSourcing.Core.Models;

public sealed class StateMachineDefinition
{
    public string Id { get; set; } = string.Empty;
    public string StateData { get; set; } = string.Empty;
    public List<string> Projections { get; set; } = [ ];
    public Dictionary<string, StateMachineEventDefinition> Events { get; set; } = [ ];
}

public sealed class StateMachineEventDefinition
{
    public List<string> UniqueConstraints { get; set; } = [ ];
    public List<string> Projections { get; set; } = [ ];
}
