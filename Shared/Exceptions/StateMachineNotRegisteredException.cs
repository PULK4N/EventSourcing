namespace EventSourcing.Shared.Exceptions;

public class StateMachineNotRegisteredException : Exception
{
    public StateMachineNotRegisteredException(string stateMachineName)
        : base($"StateMachine with name: {stateMachineName} not found") { }
}
