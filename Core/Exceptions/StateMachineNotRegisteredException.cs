namespace EventSourcing.Core
{
    public class StateMachineNotRegisteredException : Exception
    {
        public StateMachineNotRegisteredException(string stateMachineName)
            : base($"StateMachine with name: {stateMachineName} not found") { }
    }
}
