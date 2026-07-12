using EventSourcing.Shared.Models;

namespace EventSourcing.Shared.Interfaces;

public interface IEventProjector
{
    Task Update(params EventPayload[] payloads);
}
