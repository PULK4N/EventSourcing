using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Interfaces
{
    public interface IHookScheduler
    {
        Task GetHooksByEvents(List<EventPayload> payloads);
    }
}
