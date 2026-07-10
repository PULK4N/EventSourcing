namespace EventSourcing.Core.Interfaces
{
    public interface IStateDataProvider
    {
        Task<object> GetStateDataByStateMachine(string stateMachineId);
    }
}
