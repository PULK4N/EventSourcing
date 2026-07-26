using EventSourcing.Shared.Models;

namespace Shared.Interfaces
{
    public interface IEventValidator
    {
        EventValidationResult Validate(object stateData, EventPayload payload);
    }
}
