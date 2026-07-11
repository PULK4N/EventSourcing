using EventSourcing.Shared.Models;

namespace EventSourcing.Shared.Interfaces;

public interface IEvent
{
    object Apply(object stateData, EventExecutionInfo eventExecutionInfo);
}
