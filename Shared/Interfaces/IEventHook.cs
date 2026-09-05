using EventSourcing.Shared.Models;

namespace EventSourcing.Shared.Interfaces;

public interface IEventHook
{
    /// <summary>
    /// Handles an event. Treat the payload as read-only;
    /// mutations are not persisted by this call.
    /// </summary>
    Task Execute(EventPayload payload, CancellationToken cancellationToken = default);
}
