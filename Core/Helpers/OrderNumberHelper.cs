using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Providers
{
    public class OrderNumberHelper
    {
        public void AssignOrderNumbers(
            IEnumerable<EventPayload> existingEvents,
            IEnumerable<EventPayload> aggregateEventsToExecute
        )
        {
            uint currentLastOrderNumber = 0;
            if (existingEvents.Any())
                currentLastOrderNumber = existingEvents.Max(x => x.EventExecutionInfo.OrderNumber);

            foreach (var payload in aggregateEventsToExecute)
            {
                payload.EventExecutionInfo.OrderNumber = ++currentLastOrderNumber;
            }
        }
    }
}
