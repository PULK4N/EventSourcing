using EventSourcing.Shared.Models;
using Shared.Interfaces;

namespace EventSourcing.Core.Interfaces
{
    public interface IEventValidatorProvider
    {
        List<IPreEventValidator> GetPreEventStateValidators(EventPayload payload);
        List<IPostEventValidator> GetPostEventStateValidators(EventPayload payload);
    }
}
