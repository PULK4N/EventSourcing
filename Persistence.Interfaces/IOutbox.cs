using EventSourcing.Shared.Models;

namespace EventSourcing.Persistence.Interfaces;

public interface IOutbox
{
    Task<MessagePayload?> ReadNext(CancellationToken cancellationToken = default);
    Task UpdateCompleted(long id);
    Task UpdateFailed(long id, string error);
    Task Write(Dictionary<AggregateId, StateInfo> stateInfos);
}
