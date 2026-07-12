using EventSourcing.Core.Models;

namespace EventSourcing.Core.Interfaces;

public interface IStateMachineDefinitionProvider
{
    StateMachineDefinition Get(string stateMachineId);
    IReadOnlyCollection<StateMachineDefinition> GetAll();
}
