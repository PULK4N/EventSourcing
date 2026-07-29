namespace EventSourcing.Shared.Models
{
    /*
     * State info for a given aggregate
     * Make sure to instantiate only in State Handlers
     */
    public class StateInfo
    {
        public AggregateId AggregateId { get; set; }
        public uint CurrentOrderNumber { get; set; }
        public DateTime LastUpdateTimestamp { get; set; }
        public string StateMachineId { get; set; } = string.Empty;
        public List<EventPayload> LastExecutedPayloads { get; set; } = new List<EventPayload>();

        // State might not yet need to be implemented
        public string State { get; set; } = "NULL_STATE";
        public object StateData { get; set; }

        private StateInfo() { }

        public static StateInfo Create(
            object stateData,
            string stateMachineId,
            AggregateId aggregateId
        )
        {
            var stateInfo = new StateInfo();
            stateInfo.CurrentOrderNumber = 1;
            stateInfo.AggregateId = aggregateId;
            stateInfo.LastUpdateTimestamp = DateTime.UtcNow;
            stateInfo.StateMachineId = stateMachineId;
            stateInfo.StateData = stateData;

            return stateInfo;
        }
    }
}
