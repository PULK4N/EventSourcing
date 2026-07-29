using EventSourcing.Shared.Models;

namespace EventSourcing.Persistence.Interfaces;

public interface IEventStoreWithOutbox
{
    Task Write(Dictionary<AggregateId, StateInfo> stateInfos);

    Task<Dictionary<AggregateId, List<EventPayload>>> GetEvents(List<AggregateId> aggregateIds);
}
