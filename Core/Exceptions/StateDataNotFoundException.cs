namespace EventSourcing.Core
{
    public class StateDataTypeNotFoundException : Exception
    {
        public StateDataTypeNotFoundException(string stateDataName)
            : base(
                $"Class for the state data named {stateDataName}, could not be found. Possible error due to assembly configuration"
            ) { }
    }
}
