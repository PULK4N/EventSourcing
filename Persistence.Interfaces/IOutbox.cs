using EventSourcing.Shared.Models;

namespace EventSourcing.Persistence.Interfaces;

public interface IOutbox
{
    Task UpdateCompleted(long id);
    Task UpdateFailed(long id);
    Task Write(List<EventPayload> payloads);
}
