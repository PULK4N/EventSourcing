using EventSourcing.Shared.Interfaces;
using EventSourcing.Shared.Models;

namespace EventSourcing.Core.Interfaces;

public interface IEventHookProvider
{
    /// <summary>
    /// Selects hooks for one event without executing them. Return each hook once.
    /// Hook order is unspecified.
    /// </summary>
    Task<List<IEventHook>> GetHooks(
        EventPayload payload,
        CancellationToken cancellationToken = default);
}
