using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Interfaces
{
    public interface IStateDataProvider
    {
        Task<object> GetStateDataByStateMachine(string stateMachineId, AggregateId aggregateId);
    }
}
