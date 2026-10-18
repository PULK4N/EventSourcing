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

        /// <summary>
        /// Unique values the current state holds, owned by this aggregate. Empty when the
        /// state is deleted.
        /// </summary>
        public List<UniqueConstraint> UniqueConstraints { get; set; } =
            new List<UniqueConstraint>();

        /// <summary>
        /// True when <see cref="LastExecutedPayloads"/> left the state with different
        /// <see cref="UniqueConstraints"/> than it had before them. A new aggregate starts with
        /// no constraints. Like <see cref="LastExecutedPayloads"/>, it describes the calculation
        /// that produced this state, not the state itself.
        /// </summary>
        public bool UniqueConstraintsChanged { get; set; }

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
            var stateInfo = new StateInfo
            {
                CurrentOrderNumber = 1,
                AggregateId = aggregateId,
                LastUpdateTimestamp = DateTime.UtcNow,
                StateMachineId = stateMachineId,
                StateData = stateData
            };

            return stateInfo;
        }
    }
}
