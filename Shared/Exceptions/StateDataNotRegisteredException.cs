namespace EventSourcing.Shared.Exceptions;

public class StateDataNotRegisteredException : Exception
{
    public StateDataNotRegisteredException(string stateDataName)
        : base(
            $"Class for the state data named {stateDataName}, could not be instantiated. Possible error due to assembly configuration"
        ) { }
}
