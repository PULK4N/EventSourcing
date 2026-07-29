using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Providers
{
    public class OrderNumberHelper
    {
        public void AssignOrderNumbers(
            List<EventPayload> existingEvents,
            List<EventPayload> newEvents
        )
        {
            uint currentLastOrderNumber = 0;
            if (existingEvents.Any())
                currentLastOrderNumber = existingEvents.Max(x => x.EventExecutionInfo.OrderNumber);

            foreach (var payload in newEvents)
            {
                payload.EventExecutionInfo.OrderNumber = ++currentLastOrderNumber;
            }
        }
    }
}
