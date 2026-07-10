using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Interfaces
{
    public interface IOutbox
    {
        Task UpdateCompleted(long id);
        Task UpdateFailed(long id);
        Task Write(params EventPayload[] payloads);
    }
}
