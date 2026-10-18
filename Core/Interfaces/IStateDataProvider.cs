using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Interfaces
{
    public interface IStateDataProvider
    {
        object GetStateDataByStateMachine(string stateMachineId, AggregateId aggregateId);
    }
}
