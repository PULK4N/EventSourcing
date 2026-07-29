using EventSourcing.Shared.Models;

namespace EventSourcing.Shared.Helpers;

public static class EventPayloadHelper
{
    public static Dictionary<AggregateId, List<EventPayload>> GetPayloadsByAggregateDictionary(
        this List<EventPayload> payloads
    )
    {
        return payloads
            .GroupBy(payload => payload.EventExecutionInfo.AggregateId)
            .ToDictionary(
                group => group.First().EventExecutionInfo.AggregateId,
                group => group.OrderBy(payload => payload.EventExecutionInfo.OrderNumber).ToList()
            );
    }
}
