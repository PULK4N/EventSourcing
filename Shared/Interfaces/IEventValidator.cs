using EventSourcing.Shared.Models;

namespace Shared.Interfaces
{
    public interface IEventValidator
    {
        Task<EventValidationResult> Validate(object stateData);
    }
}
